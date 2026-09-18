using System;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Wagenheimer.IAPHelper.UI
{
    /// <summary>
    /// Prefab-friendly UI component for purchase restoration.
    /// Automatically manages platform visibility (Apple App Store requires a visible button,
    /// while Google Play / Amazon restore silently in the background).
    /// </summary>
    [AddComponentMenu("Wagenheimer/IAP Helper/IAP Restore Button")]
    [DisallowMultipleComponent]
    public class IAPRestoreButton : MonoBehaviour
    {
        public enum VisibilityMode
        {
            [Tooltip("Visible on iOS and macOS only (Apple App Store Guideline 3.1.1). Automatically hidden on Android/Amazon.")]
            AppleOnly,

            [Tooltip("Always visible regardless of platform.")]
            AlwaysShow,

            [Tooltip("Always hidden.")]
            Hidden
        }

        #region Inspector Fields

        [Header("Platform Policy")]
        [Tooltip("Visibility rule based on platform guidelines. Apple App Store mandates a visible button; Google Play/Amazon recommend silent auto-restore.")]
        public VisibilityMode visibilityMode = VisibilityMode.AppleOnly;

        [Header("UI Bindings")]
        [Tooltip("Button that triggers purchase restoration.")]
        public Button buttonRestore;

        [Tooltip("Optional CanvasGroup displayed while restoration is executing.")]
        public CanvasGroup loadingIndicator;

        [Header("Events")]
        [Tooltip("Dispatched when restoration succeeds.")]
        public UnityEvent onRestoreSuccess = new UnityEvent();

        [Tooltip("Dispatched if restoration encounters an error.")]
        public UnityEvent<string> onRestoreFailed = new UnityEvent<string>();

        #endregion

        #region Private Fields

        private bool _isRestoring;

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            if (buttonRestore == null)
                buttonRestore = GetComponent<Button>() ?? GetComponentInChildren<Button>();

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

        protected virtual void OnEnable()
        {
            ApplyPlatformVisibility();

            if (buttonRestore != null)
            {
                buttonRestore.onClick.RemoveListener(RestorePurchases);
                buttonRestore.onClick.AddListener(RestorePurchases);
            }
        }

        protected virtual void OnDisable()
        {
            if (buttonRestore != null)
                buttonRestore.onClick.RemoveListener(RestorePurchases);

            SetLoadingState(false);
            _isRestoring = false;
        }

        #endregion

        #region Visibility

        public void ApplyPlatformVisibility()
        {
            switch (visibilityMode)
            {
                case VisibilityMode.AppleOnly:
                    bool isApple = Application.platform == RuntimePlatform.IPhonePlayer ||
                                   Application.platform == RuntimePlatform.OSXPlayer;
                    gameObject.SetActive(isApple);
                    break;

                case VisibilityMode.AlwaysShow:
                    gameObject.SetActive(true);
                    break;

                case VisibilityMode.Hidden:
                    gameObject.SetActive(false);
                    break;
            }
        }

        #endregion

        #region Restore Flow

        public virtual async void RestorePurchases()
        {
            if (_isRestoring)
            {
                Debug.LogWarning("[IAPRestoreButton] Restoration already in progress.");
                return;
            }

            var helper = IAPHelper.Instance;
            if (helper == null)
            {
                Debug.LogError("[IAPRestoreButton] Cannot restore: IAPHelper instance not found.");
                onRestoreFailed?.Invoke("IAPHelper not initialized");
                return;
            }

            _isRestoring = true;
            SetLoadingState(true);

            try
            {
                bool ready = await helper.EnsureInitializedAsync();
                if (!ready)
                {
                    SetLoadingState(false);
                    _isRestoring = false;
                    Debug.LogError("[IAPRestoreButton] IAPHelper failed to initialize.");
                    onRestoreFailed?.Invoke("Store connection unavailable");
                    return;
                }

                helper.RestorePurchases((success, error) =>
                {
                    SetLoadingState(false);
                    _isRestoring = false;

                    if (success)
                    {
                        Debug.Log("[IAPRestoreButton] Purchases restored successfully.");
                        onRestoreSuccess?.Invoke();
                    }
                    else
                    {
                        Debug.LogError($"[IAPRestoreButton] Purchase restoration failed: {error}");
                        onRestoreFailed?.Invoke(error ?? "Restoration failed");
                    }
                });
            }
            catch (Exception ex)
            {
                SetLoadingState(false);
                _isRestoring = false;
                Debug.LogError($"[IAPRestoreButton] Exception during restore: {ex.Message}");
                onRestoreFailed?.Invoke(ex.Message);
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
public class IAPRestoreButton : Wagenheimer.IAPHelper.UI.IAPRestoreButton
{
}
