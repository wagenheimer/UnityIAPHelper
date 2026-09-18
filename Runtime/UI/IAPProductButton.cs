using System;
using System.Threading.Tasks;

using TMPro;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Wagenheimer.IAPHelper.UI
{
    /// <summary>
    /// Prefab-friendly UI component that binds a product to visual elements.
    /// Handles price display, title, description, purchase triggers, loading state,
    /// and automatic disabling/badging when non-consumables are already owned.
    /// </summary>
    [AddComponentMenu("Wagenheimer/IAP Helper/IAP Product Button")]
    [DisallowMultipleComponent]
    public class IAPProductButton : MonoBehaviour
    {
        public enum OwnedBehavior
        {
            DisableButton,
            HideButton,
            HideGameObject,
            KeepEnabled
        }

        #region Inspector Fields

        [Header("Product Identification")]
        [Tooltip("Product ID matching an entry in IAPHelper.products catalog.")]
        public string productId = "unlockfullgame";

        [Header("UI Bindings (Auto-resolved if unassigned)")]
        [Tooltip("Button that triggers the purchase flow.")]
        public Button buttonBuy;

        [Tooltip("Label for the localized price string (e.g. '$2.99').")]
        public TextMeshProUGUI labelPrice;

        [Tooltip("Optional label for the product title.")]
        public TextMeshProUGUI labelTitle;

        [Tooltip("Optional label for the product description.")]
        public TextMeshProUGUI labelDescription;

        [Tooltip("Optional icon image for the product.")]
        public Image imageIcon;

        [Tooltip("Optional GameObject shown only when this product is owned (e.g. 'Purchased' badge/checkmark).")]
        public GameObject ownedBadge;

        [Tooltip("Optional CanvasGroup displayed while a purchase is in progress.")]
        public CanvasGroup loadingIndicator;

        [Header("Non-Consumable Owned State")]
        [Tooltip("Action taken when the player already owns this non-consumable product.")]
        public OwnedBehavior ownedStateBehavior = OwnedBehavior.DisableButton;

        [Tooltip("Text assigned to labelPrice or button when owned (if behavior keeps button visible).")]
        public string ownedText = "Owned";

        [Header("Events")]
        [Tooltip("Invoked upon successful purchase confirmation or when already owned.")]
        public UnityEvent onPurchaseSuccess = new UnityEvent();

        [Tooltip("Invoked if the purchase fails or is cancelled by the user.")]
        public UnityEvent<string> onPurchaseFailed = new UnityEvent<string>();

        #endregion

        #region Private Fields

        private bool _isPurchasing;

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            AutoResolveComponents();
        }

        protected virtual void OnEnable()
        {
            SubscribeToEvents();
            RefreshUI();
        }

        protected virtual void OnDisable()
        {
            UnsubscribeFromEvents();
            SetLoadingState(false);
            _isPurchasing = false;
        }

        #endregion

        #region Setup & Auto-Resolution

        public void AutoResolveComponents()
        {
            if (buttonBuy == null)
                buttonBuy = GetComponent<Button>() ?? GetComponentInChildren<Button>();

            if (labelPrice == null)
            {
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t.name.ToLower().Contains("price"))
                    {
                        labelPrice = t;
                        break;
                    }
                }
                if (labelPrice == null && texts.Length > 0)
                    labelPrice = texts[0];
            }

            if (labelTitle == null)
            {
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t.name.ToLower().Contains("title") || t.name.ToLower().Contains("name"))
                    {
                        labelTitle = t;
                        break;
                    }
                }
            }

            if (loadingIndicator == null)
            {
                var cgs = GetComponentsInChildren<CanvasGroup>(true);
                foreach (var cg in cgs)
                {
                    if (cg.name.ToLower().Contains("wait") || cg.name.ToLower().Contains("load"))
                    {
                        loadingIndicator = cg;
                        break;
                    }
                }
            }
        }

        private void SubscribeToEvents()
        {
            if (buttonBuy != null)
            {
                buttonBuy.onClick.RemoveListener(BuyNow);
                buttonBuy.onClick.AddListener(BuyNow);
            }

            var helper = IAPHelper.Instance;
            if (helper != null)
            {
                helper.OnInitialized += RefreshUI;
                helper.OnProductsFetched += _ => RefreshUI();
                helper.OnEntitlementGranted += OnEntitlementGranted;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (buttonBuy != null)
                buttonBuy.onClick.RemoveListener(BuyNow);

            var helper = IAPHelper.Instance;
            if (helper != null)
            {
                helper.OnInitialized -= RefreshUI;
                helper.OnProductsFetched -= _ => RefreshUI();
                helper.OnEntitlementGranted -= OnEntitlementGranted;
            }
        }

        private void OnEntitlementGranted(string grantedId)
        {
            if (grantedId == productId)
                RefreshUI();
        }

        #endregion

        #region UI Refresh

        public virtual void RefreshUI()
        {
            if (string.IsNullOrEmpty(productId))
                return;

            var helper = IAPHelper.Instance;
            bool isOwned = helper != null && helper.HasPurchased(productId);

            // 1. Update Title & Description
            if (labelTitle != null)
            {
                string title = helper != null ? helper.GetProductTitle(productId) : productId;
                if (!string.IsNullOrEmpty(title))
                    labelTitle.text = title;
            }

            if (labelDescription != null && helper != null)
            {
                string desc = helper.GetProductDescription(productId);
                if (!string.IsNullOrEmpty(desc))
                    labelDescription.text = desc;
            }

            // 2. Update Price
            if (labelPrice != null)
            {
                if (isOwned && !string.IsNullOrEmpty(ownedText))
                {
                    labelPrice.text = ownedText;
                }
                else
                {
                    string price = helper != null ? helper.GetPrice(productId) : string.Empty;
                    if (!string.IsNullOrEmpty(price))
                        labelPrice.text = price;
                }
            }

            // 3. Update Owned Badge
            if (ownedBadge != null)
            {
                ownedBadge.SetActive(isOwned);
            }

            // 4. Update Owned Behavior
            if (isOwned)
            {
                ApplyOwnedBehavior();
            }
            else
            {
                if (buttonBuy != null)
                    buttonBuy.interactable = true;
                gameObject.SetActive(true);
            }
        }

        private void ApplyOwnedBehavior()
        {
            switch (ownedStateBehavior)
            {
                case OwnedBehavior.DisableButton:
                    if (buttonBuy != null)
                        buttonBuy.interactable = false;
                    break;

                case OwnedBehavior.HideButton:
                    if (buttonBuy != null)
                        buttonBuy.gameObject.SetActive(false);
                    break;

                case OwnedBehavior.HideGameObject:
                    gameObject.SetActive(false);
                    break;

                case OwnedBehavior.KeepEnabled:
                    break;
            }
        }

        #endregion

        #region Purchase Flow

        public virtual async void BuyNow()
        {
            if (_isPurchasing)
            {
                Debug.LogWarning($"[IAPProductButton] Purchase already in progress for '{productId}'.");
                return;
            }

            var helper = IAPHelper.Instance;
            if (helper == null)
            {
                Debug.LogError("[IAPProductButton] Cannot purchase: IAPHelper instance not found.");
                onPurchaseFailed?.Invoke("IAPHelper not initialized");
                return;
            }

            _isPurchasing = true;
            SetLoadingState(true);

            try
            {
                bool ready = await helper.EnsureInitializedAsync();
                if (!ready)
                {
                    SetLoadingState(false);
                    _isPurchasing = false;
                    Debug.LogError("[IAPProductButton] IAPHelper failed to initialize.");
                    onPurchaseFailed?.Invoke("Store connection unavailable");
                    return;
                }

                var result = await helper.PurchaseAsync(productId);

                SetLoadingState(false);
                _isPurchasing = false;

                if (result.IsSuccess || result.IsAlreadyOwned)
                {
                    RefreshUI();
                    onPurchaseSuccess?.Invoke();
                }
                else
                {
                    string error = result.ErrorMessage ?? result.FailureReason?.ToString() ?? "Purchase failed";
                    onPurchaseFailed?.Invoke(error);
                }
            }
            catch (Exception ex)
            {
                SetLoadingState(false);
                _isPurchasing = false;
                Debug.LogError($"[IAPProductButton] Exception during purchase: {ex.Message}");
                onPurchaseFailed?.Invoke(ex.Message);
            }
        }

        private void SetLoadingState(bool loading)
        {
            if (loadingIndicator == null) return;

            loadingIndicator.gameObject.SetActive(loading);
            loadingIndicator.alpha = loading ? 1f : 0f;
            loadingIndicator.interactable = loading;
            loadingIndicator.blocksRaycasts = loading;
        }

        #endregion
    }
}

/// <summary>
/// Global namespace alias for convenience in inspector and prefab references.
/// </summary>
public class IAPProductButton : Wagenheimer.IAPHelper.UI.IAPProductButton
{
}
