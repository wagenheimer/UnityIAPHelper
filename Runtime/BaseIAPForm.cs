using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
                IAPLog.Info($"[{GetType().Name}] Initializing form...");

                ConfigureRestoreButton();
                await InitializeIAPAndLoadPrice();
            }
            catch (Exception ex)
            {
                IAPLog.Error($"[{GetType().Name}] Error in OnEnable: {ex.Message}\n{ex.StackTrace}");
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
                IAPLog.Error($"[{GetType().Name}] IAPHelper not found!");
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
            string price = _iapHelper.GetPrice(productId);

            if (product == null && string.IsNullOrEmpty(price))
            {
                SetPriceText(Translate("productnotfound"));
                return;
            }

            if (_iapHelper.HasPurchased(productId))
            {
                IAPLog.Info($"[{GetType().Name}] Product already owned: {productId}");
                SetPriceText(Translate("purchased"));
                OnProductAlreadyOwned();
                return;
            }

            SetPriceText(!string.IsNullOrEmpty(price) ? price : (product?.metadata?.localizedPriceString ?? ""));
            IAPLog.Info($"[{GetType().Name}] Product: {productId} - Price: {price}");
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
            IAPLog.Info($"[{GetType().Name}] Product already owned by the user.");
        }

        #endregion

        #region Purchase Flow

        public virtual async void BuyNow()
        {
            if (_isPurchasing)
            {
                IAPLog.Warning($"[{GetType().Name}] Purchase already in progress.");
                return;
            }

            _iapHelper ??= IAPHelper.Instance;
            var ready = await _iapHelper.EnsureInitializedAsync();

            if (!ready)
            {
                IAPLog.Error($"[{GetType().Name}] IAP not initialized.");
                ShowError(Translate("iapnotready"));
                return;
            }

            try
            {
                IAPLog.Info($"[{GetType().Name}] Starting purchase: {productId}");

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
                    IAPLog.Info($"[{GetType().Name}] User cancelled the purchase.");
                    return;
                }

                string errorMsg = GetErrorMessage(result.FailureReason ?? PurchaseFailureReason.Unknown);
                ShowError(errorMsg);
            }
            catch (Exception ex)
            {
                IAPLog.Error($"[{GetType().Name}] Error while purchasing: {ex.Message}");
                _isPurchasing = false;
                HidePleaseWait();
                ShowError(Translate("purchasefailed"));
            }
        }

        protected abstract void GrantPurchasedContent();

        protected virtual void OnPurchaseSuccess()
        {
            IAPLog.Info($"[{GetType().Name}] Purchase completed successfully!");
        }

        /// <summary>
        /// Called once after a user-triggered Restore found this form's product and
        /// <see cref="GrantPurchasedContent"/> ran. Override to close the dialog or refresh the UI.
        /// Unlike <see cref="OnProductAlreadyOwned"/>, it is NOT called when the form opens with the
        /// product already owned, so it is safe to close the form here.
        /// </summary>
        protected virtual void OnRestoreSuccess()
        {
            IAPLog.Info($"[{GetType().Name}] Purchase restored successfully!");
        }

        #endregion

        #region Restore Purchases

        public virtual async void RestorePurchases()
        {
            if (_isPurchasing)
            {
                IAPLog.Warning($"[{GetType().Name}] Operation already in progress.");
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
                            OnRestoreSuccess();
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
                IAPLog.Error($"[{GetType().Name}] Error while restoring: {ex.Message}");
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
            IAPLog.Info($"[{GetType().Name}] SUCCESS: {message}");
        }

        protected virtual void ShowError(string message)
        {
            if (OnShowErrorNotification != null)
            {
                IAPLog.Error($"[{GetType().Name}] ERROR shown to the player via popup hook: {message}");
                OnShowErrorNotification.Invoke(message);
            }
            else
            {
                IAPLog.Error($"[{GetType().Name}] ERROR (console only, OnShowErrorNotification is not set): {message}");
            }
        }

        #endregion

        #region Localization Helper

        protected static string Translate(string key)
        {
            if (LocalizationResolver != null)
            {
                string custom = null;
                try
                {
                    custom = LocalizationResolver(key);
                }
                catch (Exception ex)
                {
                    IAPLog.Error($"[BaseIAPForm] LocalizationResolver threw for '{key}': {ex.Message}");
                }

                if (!string.IsNullOrEmpty(custom))
                {
                    IAPLog.Info($"[BaseIAPForm] Translate('{key}') = '{custom}' (via LocalizationResolver)");
                    return custom;
                }

                IAPLog.Warning($"[BaseIAPForm] LocalizationResolver returned nothing for '{key}', trying I2 reflection.");
            }
            else
            {
                IAPLog.Info($"[BaseIAPForm] LocalizationResolver is not set; trying I2 reflection for '{key}'.");
            }

            try
            {
                var locType = Type.GetType("I2.Loc.LocalizationManager, Assembly-CSharp");
                if (locType != null)
                {
                    // I2's GetTranslation has optional parameters, so an exact (string) signature lookup
                    // fails: find it by name and fill the optional ones with their defaults.
                    var method = FindTranslationMethod(locType, "GetTranslation")
                              ?? FindTranslationMethod(locType, "GetTermTranslation");
                    if (method != null)
                    {
                        var parameters = method.GetParameters();
                        var args = new object[parameters.Length];
                        args[0] = key;
                        for (int i = 1; i < args.Length; i++)
                            args[i] = Type.Missing;

                        var res = method.Invoke(null, BindingFlags.Default, null, args, null) as string;
                        if (!string.IsNullOrEmpty(res))
                        {
                            IAPLog.Info($"[BaseIAPForm] Translate('{key}') = '{res}' (via I2 reflection)");
                            return res;
                        }

                        IAPLog.Warning($"[BaseIAPForm] I2 returned an empty translation for '{key}'.");
                    }
                    else
                    {
                        IAPLog.Warning("[BaseIAPForm] I2 LocalizationManager found, but no GetTranslation/GetTermTranslation(string, ...) method.");
                    }
                }
                else
                {
                    IAPLog.Warning("[BaseIAPForm] I2 LocalizationManager type not found in Assembly-CSharp (it may live in another assembly): set BaseIAPForm.LocalizationResolver.");
                }
            }
            catch (Exception ex)
            {
                IAPLog.Error($"[BaseIAPForm] I2 reflection failed for '{key}': {ex.Message}");
            }

            IAPLog.Warning($"[BaseIAPForm] No translation for '{key}': showing the raw key.");
            return key;
        }

        private static MethodInfo FindTranslationMethod(Type type, string name)
        {
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != name || m.ReturnType != typeof(string))
                    continue;

                var ps = m.GetParameters();
                if (ps.Length == 0 || ps[0].ParameterType != typeof(string))
                    continue;

                bool restOptional = true;
                for (int i = 1; i < ps.Length; i++)
                    restOptional &= ps[i].IsOptional;

                if (restOptional)
                    return m;
            }

            return null;
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
