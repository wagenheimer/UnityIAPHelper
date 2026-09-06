using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;

using TMPro;

using UnityEngine;
using UnityEngine.Purchasing;

namespace Wagenheimer.IAPHelper
{
    /// <summary>
    /// Reusable base UI form for In-App Purchases. Decoupled from specific third-party localization
    /// or tweening libraries, with built-in fallback hooks for I2 Localization and custom error dialogs.
    /// </summary>
    public abstract class BaseIAPForm : MonoBehaviour
    {
        #region Static Hooks

        /// <summary>
        /// Optional custom localization hook (e.g. key => LocalizationManager.GetTranslation(key)).
        /// </summary>
        public static Func<string, string> LocalizationResolver;

        /// <summary>
        /// Optional hook for routing error messages to a game-specific dialog (e.g. msg => Main.main.formError.ShowError(msg)).
        /// </summary>
        public static Action<string> OnShowErrorNotification;

        #endregion

        #region Inspector Fields

        [Header("UI Elements")]
        public TextMeshProUGUI labelPrice;
        public RectTransform btRestorePurchases;
        public CanvasGroup PleaseWait;

        [Header("Settings")]
        public string productId;

        [Header("Animation")]
        public float fadeInDuration = 0.3f;
        public float fadeOutDuration = 0.3f;

        #endregion

        #region Private Fields

        protected IAPHelper _iapHelper;
        private bool _isPurchasing;
        private Coroutine _fadeCoroutine;

        #endregion

        #region Unity Lifecycle

        protected virtual async void OnEnable()
        {
            try
            {
                Debug.Log($"[{GetType().Name}] Inicializando formulário...");

                ConfigureRestoreButton();
                await InitializeIAPAndLoadPrice();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{GetType().Name}] Erro no OnEnable: {ex.Message}\n{ex.StackTrace}");
                ShowError(Translate("error"));
            }
        }

        protected virtual void OnDisable()
        {
            HidePleaseWait();
            _isPurchasing = false;
        }

        #endregion

        #region Initialization

        protected virtual async Task InitializeIAPAndLoadPrice()
        {
            _iapHelper = IAPHelper.Instance;

            if (_iapHelper == null)
            {
                Debug.LogError($"[{GetType().Name}] IAPHelper não encontrado!");
                SetPriceText(Translate("iapnotready"));
                return;
            }

            SetPriceText(Translate("loading"));

            var ready = await _iapHelper.EnsureInitializedAsync();
            if (!ready)
            {
                SetPriceText(Translate("iapnotready"));
                return;
            }

            var product = _iapHelper.GetProduct(productId);
            if (product == null)
            {
                SetPriceText(Translate("productnotfound"));
                return;
            }

            if (_iapHelper.HasPurchased(productId))
            {
                Debug.Log($"[{GetType().Name}] Produto já adquirido: {productId}");
                SetPriceText(Translate("purchased"));
                OnProductAlreadyOwned();
                return;
            }

            SetPriceText(product.metadata?.localizedPriceString ?? "");
            Debug.Log($"[{GetType().Name}] Produto: {productId} - {product.metadata?.localizedPriceString}");
        }

        protected virtual void ConfigureRestoreButton()
        {
            if (btRestorePurchases == null)
                return;

            bool showRestore = Application.platform == RuntimePlatform.IPhonePlayer ||
                              Application.platform == RuntimePlatform.OSXPlayer;

            btRestorePurchases.gameObject.SetActive(showRestore);
        }

        protected virtual void SetPriceText(string text)
        {
            if (labelPrice != null)
                labelPrice.text = text;
        }

        protected virtual void OnProductAlreadyOwned()
        {
            Debug.Log($"[{GetType().Name}] Produto já pertence ao usuário.");
        }

        #endregion

        #region Purchase Flow

        public virtual async void BuyNow()
        {
            if (_isPurchasing)
            {
                Debug.LogWarning($"[{GetType().Name}] Compra já em andamento.");
                return;
            }

            _iapHelper ??= IAPHelper.Instance;
            var ready = await _iapHelper.EnsureInitializedAsync();

            if (!ready)
            {
                Debug.LogError($"[{GetType().Name}] IAP não inicializado.");
                ShowError(Translate("iapnotready"));
                return;
            }

            try
            {
                Debug.Log($"[{GetType().Name}] Iniciando compra: {productId}");

                _isPurchasing = true;
                ShowPleaseWait();

                var result = await _iapHelper.PurchaseAsync(productId, GrantPurchasedContent);

                HidePleaseWait();
                _isPurchasing = false;

                if (result.IsSuccess || result.IsAlreadyOwned)
                {
                    GrantPurchasedContent();
                    ShowSuccess(Translate("purchasecompleted"));
                    OnPurchaseSuccess();
                    return;
                }

                if (result.FailureReason == PurchaseFailureReason.UserCancelled)
                {
                    Debug.Log($"[{GetType().Name}] Usuário cancelou a compra.");
                    return;
                }

                string errorMsg = GetErrorMessage(result.FailureReason ?? PurchaseFailureReason.Unknown);
                ShowError(errorMsg);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{GetType().Name}] Erro ao comprar: {ex.Message}");
                _isPurchasing = false;
                HidePleaseWait();
                ShowError(Translate("purchasefailed"));
            }
        }

        protected abstract void GrantPurchasedContent();

        protected virtual void OnPurchaseSuccess()
        {
            Debug.Log($"[{GetType().Name}] Compra finalizada com sucesso!");
        }

        #endregion

        #region Restore Purchases

        public virtual async void RestorePurchases()
        {
            if (_isPurchasing)
            {
                Debug.LogWarning($"[{GetType().Name}] Operação já em andamento.");
                return;
            }

            _iapHelper ??= IAPHelper.Instance;
            var ready = await _iapHelper.EnsureInitializedAsync();

            if (!ready)
            {
                ShowError(Translate("iapnotready"));
                return;
            }

            try
            {
                _isPurchasing = true;
                ShowPleaseWait();

                _iapHelper.RestorePurchases((success, error) =>
                {
                    HidePleaseWait();
                    _isPurchasing = false;

                    if (success)
                    {
                        if (_iapHelper.HasPurchased(productId))
                        {
                            GrantPurchasedContent();
                            ShowSuccess(Translate("purchaserestored"));
                            OnProductAlreadyOwned();
                        }
                        else
                        {
                            ShowError(Translate("nopreviouspurchasefound"));
                        }
                    }
                    else
                    {
                        ShowError(Translate("restorefailed"));
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{GetType().Name}] Erro ao restaurar: {ex.Message}");
                _isPurchasing = false;
                HidePleaseWait();
                ShowError(Translate("restorefailed"));
            }
        }

        #endregion

        #region UI Feedback & Animation

        protected virtual void ShowPleaseWait()
        {
            if (PleaseWait == null) return;

            PleaseWait.gameObject.SetActive(true);
            Fade(PleaseWait, 1f, fadeInDuration);
        }

        protected virtual void HidePleaseWait()
        {
            if (PleaseWait == null) return;

            Fade(PleaseWait, 0f, fadeOutDuration, () =>
            {
                if (PleaseWait != null)
                    PleaseWait.gameObject.SetActive(false);
            });
        }

        private void Fade(CanvasGroup cg, float targetAlpha, float duration, Action onComplete = null)
        {
            if (cg == null) return;

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            if (gameObject.activeInHierarchy)
            {
                _fadeCoroutine = StartCoroutine(DoFade(cg, targetAlpha, duration, onComplete));
            }
            else
            {
                cg.alpha = targetAlpha;
                onComplete?.Invoke();
            }
        }

        private IEnumerator DoFade(CanvasGroup cg, float targetAlpha, float duration, Action onComplete)
        {
            float startAlpha = cg.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            cg.alpha = targetAlpha;
            onComplete?.Invoke();
        }

        protected virtual void ShowSuccess(string message)
        {
            Debug.Log($"[{GetType().Name}] SUCESSO: {message}");
        }

        protected virtual void ShowError(string message)
        {
            if (OnShowErrorNotification != null)
            {
                OnShowErrorNotification.Invoke(message);
            }
            else
            {
                Debug.LogError($"[{GetType().Name}] ERRO: {message}");
            }
        }

        #endregion

        #region Localization Helper

        protected static string Translate(string key)
        {
            if (LocalizationResolver != null)
            {
                var custom = LocalizationResolver(key);
                if (!string.IsNullOrEmpty(custom))
                    return custom;
            }

            try
            {
                var locType = Type.GetType("I2.Loc.LocalizationManager, Assembly-CSharp");
                if (locType != null)
                {
                    var method = locType.GetMethod("GetTranslation", new[] { typeof(string) })
                              ?? locType.GetMethod("GetTermTranslation", new[] { typeof(string) });
                    if (method != null)
                    {
                        var res = method.Invoke(null, new object[] { key }) as string;
                        if (!string.IsNullOrEmpty(res))
                            return res;
                    }
                }
            }
            catch { }

            return key;
        }

        protected virtual string GetErrorMessage(PurchaseFailureReason reason)
        {
            string termKey = reason switch
            {
                PurchaseFailureReason.PurchasingUnavailable => "purchasingunavailable",
                PurchaseFailureReason.ExistingPurchasePending => "purchasepending",
                PurchaseFailureReason.ProductUnavailable => "productunavailable",
                PurchaseFailureReason.SignatureInvalid => "signatureinvalid",
                PurchaseFailureReason.UserCancelled => "purchasecancelled",
                PurchaseFailureReason.PaymentDeclined => "paymentdeclined",
                PurchaseFailureReason.DuplicateTransaction => "duplicatetransaction",
                _ => "purchasefailed"
            };

            return Translate(termKey);
        }

        #endregion
    }
}

/// <summary>
/// Backward compatibility wrapper in the global namespace.
/// </summary>
public abstract class BaseIAPForm : Wagenheimer.IAPHelper.BaseIAPForm
{
}
