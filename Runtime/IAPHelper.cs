using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Purchasing;

namespace Wagenheimer.IAPHelper
{
    /// <summary>
    /// Production-ready In-App Purchases helper for Unity IAP v5 (com.unity.purchasing 5.4.3+).
    /// Implements the mandatory two-step purchase flow (Pending -> Confirm), robust event handling,
    /// and cross-platform store product resolution.
    /// </summary>
    public class IAPHelper : MonoBehaviour
    {
        #region Singleton

        public static IAPHelper Instance { get; private set; }

        /// <summary>
        /// Optional hook for external game save systems to report owned products (e.g. SaveData.UnlockedGame).
        /// </summary>
        public static Func<string, bool> HasPurchasedFallback;

        #endregion

        #region Store Controller

        private StoreController _storeController;
        private bool _initializing;

        #endregion

        #region Configuration

        [Header("Products Configuration")]
        public List<ProductConfig> products = new List<ProductConfig>
        {
            new ProductConfig
            {
                id = "unlockfullgame",
                type = ProductType.NonConsumable,
                amazonId = "unlockfullgameamazon"
            }
        };

        [Header("Settings")]
        public bool initializeOnStart = true;
        public bool autoRestorePurchases = false;
        public bool processPendingOnFetch = true;
        public bool logPurchasesFetchFailures = false;

        #endregion

        #region Events

        public event Action OnInitialized;
        public event Action<string> OnConnectionFailed;
        public event Action<List<Product>> OnProductsFetched;
        public event Action<string> OnProductsFetchFailed;
        public event Action<Orders> OnPurchasesFetched;
        public event Action<string> OnPurchasesFetchFailed;
        public event Action<PendingOrder> OnPurchasePending;
        public event Action<Order> OnPurchaseConfirmed;
        public event Action<FailedOrder> OnPurchaseFailed;
        public event Action<DeferredOrder> OnPurchaseDeferred;
        public event Action<Entitlement> OnCheckEntitlement;

        #endregion

        #region Properties

        public bool IsInitialized { get; private set; }
        public bool IsConnected { get; private set; }
        public bool ProductsLoaded { get; private set; }

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void Start()
        {
            if (initializeOnStart)
            {
                Initialize();
            }
        }

        public async Task<bool> EnsureInitializedAsync(float timeoutSeconds = 10f)
        {
            if (IsInitialized)
                return true;

            var tcs = new TaskCompletionSource<bool>();
            Action onInit = null;
            Action<string> onFail = null;

            onInit = () =>
            {
                OnInitialized -= onInit;
                OnConnectionFailed -= onFail;
                tcs.TrySetResult(true);
            };

            onFail = _ =>
            {
                OnInitialized -= onInit;
                OnConnectionFailed -= onFail;
                tcs.TrySetResult(false);
            };

            OnInitialized += onInit;
            OnConnectionFailed += onFail;

            Initialize();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using (cts.Token.Register(() => tcs.TrySetResult(false)))
            {
                var result = await tcs.Task.ConfigureAwait(false);
                OnInitialized -= onInit;
                OnConnectionFailed -= onFail;
                return result;
            }
        }

        protected virtual void OnDestroy()
        {
            UnregisterEvents();
        }

        #endregion

        #region Initialization

        public async void Initialize()
        {
            if (IsInitialized)
            {
                Debug.LogWarning("[IAPHelper] Já está inicializado.");
                return;
            }

            if (_initializing)
            {
                Debug.Log("[IAPHelper] Inicialização já em andamento.");
                return;
            }

            _initializing = true;

            try
            {
                Debug.Log("[IAPHelper] Inicializando StoreController v5...");

                _storeController = UnityIAPServices.StoreController();

                RegisterEvents();

                _storeController.ProcessPendingOrdersOnPurchasesFetched(processPendingOnFetch);

                Debug.Log("[IAPHelper] Conectando à loja...");
                await _storeController.Connect();
            }
            catch (Exception ex)
            {
                _initializing = false;
                Debug.LogError($"[IAPHelper] Exceção durante a inicialização: {ex.Message}\n{ex.StackTrace}");
                OnConnectionFailed?.Invoke(ex.Message);
            }
        }

        private void RegisterEvents()
        {
            if (_storeController == null) return;

            _storeController.OnStoreConnected += HandleStoreConnected;
            _storeController.OnStoreDisconnected += HandleStoreDisconnected;

            _storeController.OnProductsFetched += HandleProductsFetched;
            _storeController.OnProductsFetchFailed += HandleProductsFetchFailed;

            _storeController.OnPurchasesFetched += HandlePurchasesFetched;
            _storeController.OnPurchasesFetchFailed += HandlePurchasesFetchFailed;

            _storeController.OnPurchasePending += HandlePurchasePending;
            _storeController.OnPurchaseConfirmed += HandlePurchaseConfirmed;
            _storeController.OnPurchaseFailed += HandlePurchaseFailed;
            _storeController.OnPurchaseDeferred += HandlePurchaseDeferred;

            _storeController.OnCheckEntitlement += HandleCheckEntitlement;

            _storeController.OnAuthAccountChanged += HandleAuthAccountChanged;
        }

        private void UnregisterEvents()
        {
            if (_storeController == null) return;

            _storeController.OnStoreConnected -= HandleStoreConnected;
            _storeController.OnStoreDisconnected -= HandleStoreDisconnected;

            _storeController.OnProductsFetched -= HandleProductsFetched;
            _storeController.OnProductsFetchFailed -= HandleProductsFetchFailed;

            _storeController.OnPurchasesFetched -= HandlePurchasesFetched;
            _storeController.OnPurchasesFetchFailed -= HandlePurchasesFetchFailed;

            _storeController.OnPurchasePending -= HandlePurchasePending;
            _storeController.OnPurchaseConfirmed -= HandlePurchaseConfirmed;
            _storeController.OnPurchaseFailed -= HandlePurchaseFailed;
            _storeController.OnPurchaseDeferred -= HandlePurchaseDeferred;

            _storeController.OnCheckEntitlement -= HandleCheckEntitlement;

            _storeController.OnAuthAccountChanged -= HandleAuthAccountChanged;
        }

        #endregion

        #region Store Event Handlers

        private void HandleStoreConnected()
        {
            IsConnected = true;
            Debug.Log("[IAPHelper] Conectado à loja com sucesso.");

            FetchProducts();

            if (autoRestorePurchases)
            {
                FetchPurchases();
            }
        }

        private void HandleStoreDisconnected(StoreConnectionFailureDescription failure)
        {
            IsConnected = false;
            _initializing = false;
            Debug.LogError($"[IAPHelper] Desconectado da loja: {failure.message}");
            OnConnectionFailed?.Invoke(failure.message);
        }

        private void HandleAuthAccountChanged()
        {
            Debug.Log("[IAPHelper] Conta de autenticação alterada. Re-buscando catálogo...");
            FetchProducts();
            FetchPurchases();
        }

        #endregion

        #region Products Management

        public void FetchProducts()
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[IAPHelper] Não é possível buscar produtos: loja não conectada.");
                return;
            }

            try
            {
                Debug.Log("[IAPHelper] Buscando produtos...");

                var productDefinitions = new List<ProductDefinition>();
                foreach (var product in products)
                {
                    string storeSpecificId = product.id;

#if AMAZON_STORE || UNITY_AMAZON
                    if (!string.IsNullOrEmpty(product.amazonId))
                        storeSpecificId = product.amazonId;
#elif UNITY_ANDROID
                    if (!string.IsNullOrEmpty(product.googlePlayId))
                        storeSpecificId = product.googlePlayId;
#elif UNITY_IOS || UNITY_STANDALONE_OSX
                    if (!string.IsNullOrEmpty(product.appleId))
                        storeSpecificId = product.appleId;
#endif

                    if (!string.IsNullOrEmpty(storeSpecificId) && storeSpecificId != product.id)
                    {
                        productDefinitions.Add(new ProductDefinition(product.id, storeSpecificId, product.type));
                    }
                    else
                    {
                        productDefinitions.Add(new ProductDefinition(product.id, product.type));
                    }
                }

                _storeController.FetchProducts(productDefinitions);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IAPHelper] Erro ao buscar produtos: {ex.Message}");
            }
        }

        private void HandleProductsFetched(List<Product> fetchedProducts)
        {
            ProductsLoaded = true;
            IsInitialized = true;
            _initializing = false;

            Debug.Log($"[IAPHelper] Produtos carregados com sucesso: {fetchedProducts?.Count ?? 0}");

            if (fetchedProducts != null)
            {
                foreach (var p in fetchedProducts)
                {
                    Debug.Log($"  - {p.definition.id} ({p.definition.type}): {p.metadata?.localizedPriceString} (Disponível: {p.availableToPurchase})");
                }
            }

            OnProductsFetched?.Invoke(fetchedProducts);
            OnInitialized?.Invoke();
        }

        private void HandleProductsFetchFailed(ProductFetchFailed failure)
        {
            _initializing = false;
            string reason = failure?.FailureReason.ToString() ?? "Unknown";
            Debug.LogError($"[IAPHelper] Falha ao carregar produtos: {reason}");
            OnProductsFetchFailed?.Invoke(reason);
        }

        #endregion

        #region Purchases Management

        public void FetchPurchases()
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[IAPHelper] Não é possível buscar compras: loja não conectada.");
                return;
            }

            try
            {
                Debug.Log("[IAPHelper] Buscando compras existentes...");
                _storeController.FetchPurchases();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IAPHelper] Erro ao buscar compras: {ex.Message}");
            }
        }

        private void HandlePurchasesFetched(Orders orders)
        {
            int pendingCount = orders?.PendingOrders?.Count ?? 0;
            int confirmedCount = orders?.ConfirmedOrders?.Count ?? 0;

            Debug.Log($"[IAPHelper] Compras buscadas: {pendingCount} pendentes, {confirmedCount} confirmadas.");

            OnPurchasesFetched?.Invoke(orders);
        }

        private void HandlePurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            if (logPurchasesFetchFailures)
            {
                Debug.LogWarning($"[IAPHelper] Falha ao buscar compras: {failure.message} ({failure.failureReason})");
            }
            OnPurchasesFetchFailed?.Invoke(failure.message);
        }

        #endregion

        #region Two-Step Purchase Flow

        public void Purchase(string productId)
        {
            if (!IsConnected || !ProductsLoaded)
            {
                Debug.LogError("[IAPHelper] Loja não está pronta para compras.");
                return;
            }

            try
            {
                var product = GetProduct(productId);
                if (product != null)
                {
                    _storeController.PurchaseProduct(product);
                }
                else
                {
                    _storeController.PurchaseProduct(productId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IAPHelper] Erro ao comprar produto '{productId}': {ex.Message}");
            }
        }

        public async Task<PurchaseResult> PurchaseAsync(string productId, Action onGrantContent = null, float timeoutSeconds = 60f)
        {
            var tcs = new TaskCompletionSource<PurchaseResult>();

            if (HasPurchased(productId))
            {
                return new PurchaseResult { IsSuccess = true, IsAlreadyOwned = true, ProductId = productId };
            }

            Action<PendingOrder> onPending = null;
            Action<Order> onConfirmed = null;
            Action<FailedOrder> onFailed = null;

            onPending = pendingOrder =>
            {
                var id = GetOrderProductId(pendingOrder);
                if (id == productId)
                {
                    try
                    {
                        onGrantContent?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[IAPHelper] Erro ao liberar conteúdo: {ex.Message}");
                    }

                    ConfirmPurchase(pendingOrder);
                }
            };

            onConfirmed = order =>
            {
                var id = GetOrderProductId(order);
                if (id == productId)
                {
                    Cleanup();
                    if (order is ConfirmedOrder confirmed)
                    {
                        tcs.TrySetResult(new PurchaseResult { IsSuccess = true, ProductId = productId, ConfirmedOrder = confirmed });
                    }
                    else if (order is FailedOrder failedOrder)
                    {
                        tcs.TrySetResult(new PurchaseResult
                        {
                            IsSuccess = false,
                            ProductId = productId,
                            FailureReason = PurchaseFailureReason.Unknown,
                            ErrorMessage = failedOrder.Details
                        });
                    }
                }
            };

            onFailed = failedOrder =>
            {
                Cleanup();
                tcs.TrySetResult(new PurchaseResult
                {
                    IsSuccess = false,
                    ProductId = productId,
                    FailureReason = failedOrder.FailureReason,
                    ErrorMessage = failedOrder.Details
                });
            };

            void Cleanup()
            {
                OnPurchasePending -= onPending;
                OnPurchaseConfirmed -= onConfirmed;
                OnPurchaseFailed -= onFailed;
            }

            OnPurchasePending += onPending;
            OnPurchaseConfirmed += onConfirmed;
            OnPurchaseFailed += onFailed;

            Purchase(productId);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using (cts.Token.Register(() =>
            {
                Cleanup();
                tcs.TrySetResult(new PurchaseResult
                {
                    IsSuccess = false,
                    ProductId = productId,
                    ErrorMessage = "Purchase timed out"
                });
            }))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }

        private void HandlePurchasePending(PendingOrder order)
        {
            var productId = GetOrderProductId(order);
            Debug.Log($"[IAPHelper] Compra pendente para o produto: {productId}");
            OnPurchasePending?.Invoke(order);
        }

        public void ConfirmPurchase(PendingOrder order)
        {
            if (order == null)
            {
                Debug.LogError("[IAPHelper] Não é possível confirmar: PendingOrder é null!");
                return;
            }

            try
            {
                var productId = GetOrderProductId(order);
                Debug.Log($"[IAPHelper] Confirmando ordem na loja para: {productId}");
                _storeController.ConfirmPurchase(order);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IAPHelper] Erro ao confirmar compra: {ex.Message}");
            }
        }

        private void HandlePurchaseConfirmed(Order order)
        {
            var productId = GetOrderProductId(order);

            if (order is ConfirmedOrder)
            {
                Debug.Log($"[IAPHelper] Compra confirmada com sucesso: {productId}");
            }
            else if (order is FailedOrder failed)
            {
                Debug.LogError($"[IAPHelper] Confirmação da compra falhou para: {productId} - {failed.Details}");
            }

            OnPurchaseConfirmed?.Invoke(order);
        }

        private void HandlePurchaseFailed(FailedOrder failedOrder)
        {
            Debug.LogError($"[IAPHelper] Compra falhou: {failedOrder.FailureReason} - {failedOrder.Details}");
            OnPurchaseFailed?.Invoke(failedOrder);
        }

        private void HandlePurchaseDeferred(DeferredOrder deferredOrder)
        {
            Debug.Log("[IAPHelper] Compra diferida (aguardando aprovação, ex: Ask-to-Buy).");
            OnPurchaseDeferred?.Invoke(deferredOrder);
        }

        #endregion

        #region Entitlement Checking

        private void HandleCheckEntitlement(Entitlement entitlement)
        {
            Debug.Log($"[IAPHelper] Entitlement para '{entitlement.Product.definition.id}': {entitlement.Status}");
            OnCheckEntitlement?.Invoke(entitlement);
        }

        #endregion

        #region Public Methods - Queries & Entitlement

        public Product GetProduct(string productId)
        {
            if (!ProductsLoaded || _storeController == null)
                return null;

            var cachedProducts = _storeController.GetProducts();
            if (cachedProducts == null)
                return null;

            return cachedProducts.FirstOrDefault(p =>
                p.definition.id == productId ||
                p.uSku == productId ||
                (p.definition.storeSpecificId != null && p.definition.storeSpecificId == productId));
        }

        public string GetPrice(string productId)
        {
            var product = GetProduct(productId);
            return product?.metadata?.localizedPriceString ?? "";
        }

        public bool IsProductAvailable(string productId)
        {
            var product = GetProduct(productId);
            return product != null && product.availableToPurchase;
        }

        public bool HasPurchased(string productId)
        {
            if (_storeController != null)
            {
                var purchases = _storeController.GetPurchases();
                if (purchases != null)
                {
                    foreach (var order in purchases)
                    {
                        if (order is ConfirmedOrder confirmed)
                        {
                            if (confirmed.CartOrdered?.Items() != null)
                            {
                                foreach (var item in confirmed.CartOrdered.Items())
                                {
                                    if (item.Product != null && (item.Product.definition.id == productId || item.Product.uSku == productId))
                                        return true;
                                }
                            }

                            if (confirmed.Info?.PurchasedProductInfo != null)
                            {
                                foreach (var info in confirmed.Info.PurchasedProductInfo)
                                {
                                    if (info.productId == productId)
                                        return true;
                                }
                            }
                        }
                    }
                }
            }

            if (HasPurchasedFallback != null && HasPurchasedFallback(productId))
            {
                return true;
            }

            return false;
        }

        public void CheckEntitlement(string productId)
        {
            var product = GetProduct(productId);
            if (product == null)
            {
                Debug.LogWarning($"[IAPHelper] CheckEntitlement: produto '{productId}' não encontrado no cache.");
                return;
            }

            _storeController?.CheckEntitlement(product);
        }

        #endregion

        #region Public Methods - Restore

        public void RestorePurchases(Action<bool, string> onComplete = null)
        {
            Debug.Log("[IAPHelper] Restaurando compras...");

            if (_storeController == null)
            {
                Debug.LogError("[IAPHelper] Não é possível restaurar: StoreController é null.");
                onComplete?.Invoke(false, "StoreController not initialized");
                return;
            }

            if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.OSXPlayer)
            {
                _storeController.RestoreTransactions((success, error) =>
                {
                    if (success)
                        Debug.Log("[IAPHelper] RestoreTransactions concluído com sucesso.");
                    else
                        Debug.LogError($"[IAPHelper] RestoreTransactions falhou: {error}");

                    onComplete?.Invoke(success, error);
                });
            }
            else
            {
                FetchPurchases();
                onComplete?.Invoke(true, null);
            }
        }

        #endregion

        #region Helpers

        private static string GetOrderProductId(Order order)
        {
            if (order == null)
                return "unknown";

            var cartItem = order.CartOrdered?.Items()?.FirstOrDefault();
            if (cartItem?.Product != null)
            {
                return cartItem.Product.definition.id ?? cartItem.Product.uSku;
            }

            var productInfo = order.Info?.PurchasedProductInfo?.FirstOrDefault();
            if (productInfo != null)
            {
                return productInfo.productId;
            }

            return "unknown";
        }

        #endregion
    }

    [Serializable]
    public class ProductConfig
    {
        public string id = "unlockfullgame";
        public ProductType type = ProductType.NonConsumable;
        public string googlePlayId = "";
        public string appleId = "";
        public string amazonId = "unlockfullgameamazon";
    }

    public class PurchaseResult
    {
        public bool IsSuccess { get; set; }
        public bool IsAlreadyOwned { get; set; }
        public string ProductId { get; set; }
        public ConfirmedOrder ConfirmedOrder { get; set; }
        public PurchaseFailureReason? FailureReason { get; set; }
        public string ErrorMessage { get; set; }
    }
}

/// <summary>
/// Backward compatibility wrapper in the global namespace.
/// </summary>
public class IAPHelper : Wagenheimer.IAPHelper.IAPHelper
{
}
