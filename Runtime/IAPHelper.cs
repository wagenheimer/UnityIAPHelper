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

        /// <summary>
        /// Product IDs already granted in this session (prevents <see cref="OnEntitlementGranted"/> from
        /// firing repeatedly for the same purchase while reprocessing pending/confirmed orders).
        /// </summary>
        private readonly HashSet<string> _grantedProductIds = new HashSet<string>();

        private float _lastResumeFetchTime = float.NegativeInfinity;
        private const float ResumeFetchCooldownSeconds = 5f;

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

        /// <summary>
        /// When enabled, automatically calls <see cref="FetchPurchases"/> right after connecting to the store.
        /// <para><b>Android (Google Play / Amazon):</b> Strongly recommended <c>true</c> (or use <see cref="RecommendedAutoRestoreForCurrentPlatform"/>).
        /// The query is 100% silent and requires no password prompt, ensuring non-consumable purchases (e.g. Unlock Game)
        /// are restored automatically when the player reinstalls the game or switches devices, replacing the old IAPListener flow.</para>
        /// <para><b>iOS / macOS (Apple):</b> Recommended <c>false</c>. Apple's App Store Review Guidelines
        /// require purchase restoration to be triggered by an explicit user action (e.g. a 'Restore Purchases' button)
        /// to avoid unexpected Apple ID authentication prompts on launch.</para>
        /// </summary>
        [Tooltip("Automatically restores purchases on connect. Recommended TRUE on Android/Amazon and FALSE on iOS/macOS (Apple).")]
        public bool autoRestorePurchases = false;

        /// <summary>
        /// Returns the recommended autoRestorePurchases setting for the current platform:
        /// <c>true</c> for Android/Amazon (silent and required for restoring on reinstall),
        /// <c>false</c> for iOS/macOS (requires a manual button per Apple's policy).
        /// </summary>
        public static bool RecommendedAutoRestoreForCurrentPlatform =>
            Application.platform == RuntimePlatform.Android;

        /// <summary>
        /// Automatically sets <see cref="autoRestorePurchases"/> based on the current runtime platform.
        /// </summary>
        public void ConfigureAutoRestoreByPlatform()
        {
            autoRestorePurchases = RecommendedAutoRestoreForCurrentPlatform;
        }

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

        /// <summary>
        /// Fired exactly once per product, as soon as the helper determines the player owns it — whether
        /// through a live purchase (<see cref="PendingOrder"/>), a restore detected on boot
        /// (<see cref="FetchPurchases"/>), or a re-fetch triggered when the app regains focus.
        /// <para>
        /// Unlike the local callback passed to <see cref="PurchaseAsync"/>, this subscription is permanent:
        /// the game should use it (typically once, at boot) to unlock the purchased content
        /// (e.g. <c>SaveData.UnlockedGame = true</c>). This guarantees the purchase is granted even if the
        /// purchase UI has already been closed, its timeout has elapsed, or the app was minimized during the
        /// store's payment flow.
        /// </para>
        /// </summary>
        public event Action<string> OnEntitlementGranted;

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

        /// <summary>
        /// Re-fetches pending purchases when the app returns to the foreground. Covers the case where the
        /// user finishes payment in the store's native UI (Google Play / App Store), which runs on top of the
        /// app and can leave it paused/unfocused long enough for <see cref="PurchaseAsync"/>'s timeout to
        /// expire before the confirmation arrives.
        /// </summary>
        protected virtual void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
                TryRefetchPurchasesOnResume();
        }

        protected virtual void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                TryRefetchPurchasesOnResume();
        }

        private void TryRefetchPurchasesOnResume()
        {
            if (!IsConnected || !autoRestorePurchases)
                return;

            // OnApplicationPause and OnApplicationFocus tend to fire almost together on resume;
            // avoid two redundant fetches back-to-back.
            if (Time.unscaledTime - _lastResumeFetchTime < ResumeFetchCooldownSeconds)
                return;

            _lastResumeFetchTime = Time.unscaledTime;
            Debug.Log("[IAPHelper] App regained focus - re-fetching purchases (self-heal for pending orders).");
            FetchPurchases();
        }

        #endregion

        #region Initialization

        public async void Initialize()
        {
            if (IsInitialized)
            {
                Debug.LogWarning("[IAPHelper] Already initialized.");
                return;
            }

            if (_initializing)
            {
                Debug.Log("[IAPHelper] Initialization already in progress.");
                return;
            }

            _initializing = true;

            try
            {
                Debug.Log("[IAPHelper] Initializing StoreController v5...");

                _storeController = UnityIAPServices.StoreController();

                RegisterEvents();

                _storeController.ProcessPendingOrdersOnPurchasesFetched(processPendingOnFetch);

                Debug.Log("[IAPHelper] Connecting to the store...");
                await _storeController.Connect();
            }
            catch (Exception ex)
            {
                _initializing = false;
                Debug.LogError($"[IAPHelper] Exception during initialization: {ex.Message}\n{ex.StackTrace}");
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
            Debug.Log("[IAPHelper] Connected to the store successfully.");

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
            Debug.LogError($"[IAPHelper] Disconnected from the store: {failure.message}");
            OnConnectionFailed?.Invoke(failure.message);
        }

        private void HandleAuthAccountChanged()
        {
            Debug.Log("[IAPHelper] Auth account changed. Re-fetching catalog...");
            FetchProducts();
            FetchPurchases();
        }

        #endregion

        #region Products Management

        public void FetchProducts()
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[IAPHelper] Cannot fetch products: store not connected.");
                return;
            }

            try
            {
                Debug.Log("[IAPHelper] Fetching products...");

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
                Debug.LogError($"[IAPHelper] Error fetching products: {ex.Message}");
            }
        }

        private void HandleProductsFetched(List<Product> fetchedProducts)
        {
            ProductsLoaded = true;
            IsInitialized = true;
            _initializing = false;

            Debug.Log($"[IAPHelper] Products loaded successfully: {fetchedProducts?.Count ?? 0}");

            if (fetchedProducts != null)
            {
                foreach (var p in fetchedProducts)
                {
                    Debug.Log($"  - {p.definition.id} ({p.definition.type}): {p.metadata?.localizedPriceString} (Available: {p.availableToPurchase})");
                }
            }

            OnProductsFetched?.Invoke(fetchedProducts);
            OnInitialized?.Invoke();
        }

        private void HandleProductsFetchFailed(ProductFetchFailed failure)
        {
            _initializing = false;
            string reason = failure?.FailureReason.ToString() ?? "Unknown";
            Debug.LogError($"[IAPHelper] Failed to load products: {reason}");
            OnProductsFetchFailed?.Invoke(reason);
        }

        #endregion

        #region Purchases Management

        public void FetchPurchases()
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[IAPHelper] Cannot fetch purchases: store not connected.");
                return;
            }

            try
            {
                Debug.Log("[IAPHelper] Fetching existing purchases...");
                _storeController.FetchPurchases();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IAPHelper] Error fetching purchases: {ex.Message}");
            }
        }

        private void HandlePurchasesFetched(Orders orders)
        {
            int pendingCount = orders?.PendingOrders?.Count ?? 0;
            int confirmedCount = orders?.ConfirmedOrders?.Count ?? 0;

            Debug.Log($"[IAPHelper] Purchases fetched: {pendingCount} pending, {confirmedCount} confirmed.");

            if (orders != null)
            {
                // Confirmed: make sure the content was granted (idempotent, covers reinstall/device swap).
                if (orders.ConfirmedOrders != null)
                {
                    foreach (var order in orders.ConfirmedOrders)
                        GrantEntitlementIfNeeded(GetOrderProductId(order));
                }

                // Leftover pending orders from a previous session (e.g. the app was closed/killed by the OS
                // before ConfirmPurchase ran, or the original purchase's temporary listener had already
                // timed out). Without this, the order stays stuck as "Pending" in the store forever and the
                // player never receives the content even though they already paid - grant and confirm now.
                if (orders.PendingOrders != null)
                {
                    foreach (var order in orders.PendingOrders)
                    {
                        var productId = GetOrderProductId(order);
                        GrantEntitlementIfNeeded(productId);
                        ConfirmPurchase(order);
                    }
                }
            }

            OnPurchasesFetched?.Invoke(orders);
        }

        private void HandlePurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            if (logPurchasesFetchFailures)
            {
                Debug.LogWarning($"[IAPHelper] Failed to fetch purchases: {failure.message} ({failure.failureReason})");
            }
            OnPurchasesFetchFailed?.Invoke(failure.message);
        }

        #endregion

        #region Two-Step Purchase Flow

        public void Purchase(string productId)
        {
            if (!IsConnected || !ProductsLoaded)
            {
                Debug.LogError("[IAPHelper] Store is not ready for purchases.");
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
                Debug.LogError($"[IAPHelper] Error purchasing product '{productId}': {ex.Message}");
            }
        }

        /// <summary>
        /// Starts a purchase and awaits its resolution (to drive UI feedback: loading, success, error).
        /// </summary>
        /// <remarks>
        /// Granting content and confirming the order with the store no longer depend on this Task or its
        /// timeout: that's done permanently and centrally in <see cref="HandlePurchasePending"/> and in
        /// <see cref="HandlePurchasesFetched"/>, subscribed since <see cref="RegisterEvents"/>. This means
        /// that even if this call times out (<paramref name="timeoutSeconds"/> — e.g. the user took a while
        /// to complete payment in the store's UI) or the purchase form has already been closed/destroyed, the
        /// purchase will still be granted and confirmed correctly as soon as the store's event arrives -
        /// including in a future session, via <see cref="FetchPurchases"/> on boot or on app resume.
        /// <paramref name="onGrantContent"/> here only exists to sync immediate UI feedback
        /// (e.g. closing the purchase dialog) while the form is still on screen.
        /// </remarks>
        public async Task<PurchaseResult> PurchaseAsync(string productId, Action onGrantContent = null, float timeoutSeconds = 60f)
        {
            var tcs = new TaskCompletionSource<PurchaseResult>();

            if (HasPurchased(productId))
            {
                onGrantContent?.Invoke();
                return new PurchaseResult { IsSuccess = true, IsAlreadyOwned = true, ProductId = productId };
            }

            Action<string> onGranted = null;
            Action<Order> onConfirmed = null;
            Action<FailedOrder> onFailed = null;

            onGranted = grantedProductId =>
            {
                if (grantedProductId == productId)
                {
                    try
                    {
                        onGrantContent?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[IAPHelper] Error notifying UI about content grant: {ex.Message}");
                    }
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
                OnEntitlementGranted -= onGranted;
                OnPurchaseConfirmed -= onConfirmed;
                OnPurchaseFailed -= onFailed;
            }

            OnEntitlementGranted += onGranted;
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
            Debug.Log($"[IAPHelper] Pending purchase for product: {productId}");

            // Grant content and confirm the order HERE, permanently - this does not depend on any UI
            // listening. Fixes the scenario where the purchase confirms after PurchaseAsync's timeout, or
            // after the purchase form has already been closed/destroyed (e.g. app minimized during the
            // store's native checkout).
            GrantEntitlementIfNeeded(productId);
            ConfirmPurchase(order);

            OnPurchasePending?.Invoke(order);
        }

        /// <summary>
        /// Fires <see cref="OnEntitlementGranted"/> for <paramref name="productId"/> exactly once per
        /// session. Called from every code path that can indicate ownership of the product: a live purchase,
        /// a restore on boot, and a re-fetch on app resume.
        /// </summary>
        private void GrantEntitlementIfNeeded(string productId)
        {
            if (string.IsNullOrEmpty(productId) || productId == "unknown")
                return;

            if (!_grantedProductIds.Add(productId))
                return;

            Debug.Log($"[IAPHelper] Granting entitlement for: {productId}");

            try
            {
                OnEntitlementGranted?.Invoke(productId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IAPHelper] Error granting entitlement for '{productId}': {ex.Message}");
            }
        }

        public void ConfirmPurchase(PendingOrder order)
        {
            if (order == null)
            {
                Debug.LogError("[IAPHelper] Cannot confirm: PendingOrder is null!");
                return;
            }

            try
            {
                var productId = GetOrderProductId(order);
                Debug.Log($"[IAPHelper] Confirming order with the store for: {productId}");
                _storeController.ConfirmPurchase(order);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IAPHelper] Error confirming purchase: {ex.Message}");
            }
        }

        private void HandlePurchaseConfirmed(Order order)
        {
            var productId = GetOrderProductId(order);

            if (order is ConfirmedOrder)
            {
                Debug.Log($"[IAPHelper] Purchase confirmed successfully: {productId}");
            }
            else if (order is FailedOrder failed)
            {
                Debug.LogError($"[IAPHelper] Purchase confirmation failed for: {productId} - {failed.Details}");
            }

            OnPurchaseConfirmed?.Invoke(order);
        }

        private void HandlePurchaseFailed(FailedOrder failedOrder)
        {
            Debug.LogError($"[IAPHelper] Purchase failed: {failedOrder.FailureReason} - {failedOrder.Details}");
            OnPurchaseFailed?.Invoke(failedOrder);
        }

        private void HandlePurchaseDeferred(DeferredOrder deferredOrder)
        {
            Debug.Log("[IAPHelper] Purchase deferred (awaiting approval, e.g. Ask-to-Buy).");
            OnPurchaseDeferred?.Invoke(deferredOrder);
        }

        #endregion

        #region Entitlement Checking

        private void HandleCheckEntitlement(Entitlement entitlement)
        {
            Debug.Log($"[IAPHelper] Entitlement for '{entitlement.Product.definition.id}': {entitlement.Status}");
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
                Debug.LogWarning($"[IAPHelper] CheckEntitlement: product '{productId}' not found in cache.");
                return;
            }

            _storeController?.CheckEntitlement(product);
        }

        #endregion

        #region Public Methods - Restore

        public void RestorePurchases(Action<bool, string> onComplete = null)
        {
            Debug.Log("[IAPHelper] Restoring purchases...");

            if (_storeController == null)
            {
                Debug.LogError("[IAPHelper] Cannot restore: StoreController is null.");
                onComplete?.Invoke(false, "StoreController not initialized");
                return;
            }

            if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.OSXPlayer)
            {
                _storeController.RestoreTransactions((success, error) =>
                {
                    if (success)
                        Debug.Log("[IAPHelper] RestoreTransactions completed successfully.");
                    else
                        Debug.LogError($"[IAPHelper] RestoreTransactions failed: {error}");

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
