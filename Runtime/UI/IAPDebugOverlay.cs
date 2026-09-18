using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Purchasing;

namespace Wagenheimer.IAPHelper.UI
{
    /// <summary>
    /// In-game runtime debug overlay for testing multi-product IAP flows.
    /// Provides real-time store inspection, simulated purchase/restore triggers,
    /// and entitlement clearing in Development Builds and Unity Editor.
    /// </summary>
    [AddComponentMenu("Wagenheimer/IAP Helper/IAP Debug Overlay")]
    [DisallowMultipleComponent]
    public class IAPDebugOverlay : MonoBehaviour
    {
        #region Settings

        [Header("Runtime Access")]
        [Tooltip("Hot key to toggle debug panel visibility in game.")]
        public KeyCode toggleKey = KeyCode.F10;

        [Tooltip("Whether to draw a small floating 'IAP DBG' button on screen.")]
        public bool showFloatingButton = true;

        [Tooltip("Allow overlay to run even in non-development / release builds. Strongly recommended FALSE for production.")]
        public bool enableInReleaseBuilds = false;

        #endregion

        #region Private Fields

        private bool _isOpen;
        private Rect _windowRect = new Rect(10, 10, 480, 520);
        private Vector2 _scrollPos;
        private string _statusLog = "Ready.";

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (!Debug.isDebugBuild && !Application.isEditor && !enableInReleaseBuilds)
            {
                Destroy(this);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _isOpen = !_isOpen;
            }
        }

        private void OnGUI()
        {
            if (!Debug.isDebugBuild && !Application.isEditor && !enableInReleaseBuilds)
                return;

            var prevSkin = GUI.skin;
            GUI.depth = -9999;

            if (showFloatingButton && !_isOpen)
            {
                if (GUI.Button(new Rect(10, Screen.height - 40, 90, 30), "IAP DBG"))
                {
                    _isOpen = true;
                }
            }

            if (_isOpen)
            {
                _windowRect = GUI.Window(888123, _windowRect, DrawDebugWindow, "IAP Helper - In-Game Debug Panel");
            }
        }

        #endregion

        #region GUI Layout

        private void DrawDebugWindow(int windowId)
        {
            GUI.DragWindow(new Rect(0, 0, 420, 25));

            if (GUI.Button(new Rect(_windowRect.width - 55, 4, 50, 20), "Close"))
            {
                _isOpen = false;
                return;
            }

            GUILayout.Space(25);

            var helper = IAPHelper.Instance;
            if (helper == null)
            {
                GUILayout.Label("Status: <color=red>IAPHelper instance not found in scene!</color>");
                return;
            }

            // Connection Banner
            GUILayout.BeginVertical("box");
            string statusColor = helper.IsConnected ? "lime" : "yellow";
            GUILayout.Label($"<b>Store Connection:</b> <color={statusColor}>{(helper.IsConnected ? "Connected" : "Disconnected / Initializing")}</color>");
            GUILayout.Label($"<b>Products Loaded:</b> {helper.ProductsLoaded} | <b>Auto-Restore:</b> {helper.autoRestorePurchases}");
            GUILayout.Label($"<b>Platform:</b> {Application.platform}");
            GUILayout.EndVertical();

            GUILayout.Space(5);

            // Global Actions
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Force Connect / Init"))
            {
                helper.Initialize();
                _statusLog = $"[{DateTime.Now:HH:mm:ss}] Initialize called.";
            }

            if (GUILayout.Button("Fetch / Restore Now"))
            {
                helper.FetchPurchases();
                _statusLog = $"[{DateTime.Now:HH:mm:ss}] FetchPurchases requested.";
            }

            if (GUILayout.Button("Clear All Fallback Keys"))
            {
                ClearAllFallbacks(helper);
                _statusLog = $"[{DateTime.Now:HH:mm:ss}] PlayerPrefs fallbacks cleared.";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Products Section
            GUILayout.Label("<b>Configured Products Catalog:</b>");
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(260));

            if (helper.products == null || helper.products.Count == 0)
            {
                GUILayout.Label("No products configured in IAPHelper.");
            }
            else
            {
                foreach (var product in helper.products)
                {
                    DrawProductRow(helper, product);
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(5);

            // Status Log
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>Last Log:</b> {_statusLog}");
            GUILayout.EndVertical();
        }

        private void DrawProductRow(IAPHelper helper, ProductConfig product)
        {
            GUILayout.BeginVertical("box");

            bool isOwned = helper.HasPurchased(product.id);
            string ownedColor = isOwned ? "lime" : "white";
            string priceStr = helper.GetPrice(product.id);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>ID:</b> {product.id} (<color=cyan>{product.type}</color>)");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<b>Price:</b> {(!string.IsNullOrEmpty(priceStr) ? priceStr : "(loading)")}");
            GUILayout.Label($"| <b>Owned:</b> <color={ownedColor}>{(isOwned ? "YES" : "NO")}</color>");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            // Simulate Grant
            if (GUILayout.Button("Simulate Grant"))
            {
                if (!string.IsNullOrEmpty(product.playerPrefsFallbackKey))
                {
                    PlayerPrefs.SetInt(product.playerPrefsFallbackKey, 1);
                    PlayerPrefs.Save();
                }
                product.onEntitlementGranted?.Invoke();
                _statusLog = $"[{DateTime.Now:HH:mm:ss}] Simulated grant for: {product.id}";
            }

            // Simulate Revoke (for non-consumables)
            if (product.type == ProductType.NonConsumable)
            {
                if (GUILayout.Button("Revoke Fallback"))
                {
                    if (!string.IsNullOrEmpty(product.playerPrefsFallbackKey))
                    {
                        PlayerPrefs.DeleteKey(product.playerPrefsFallbackKey);
                        PlayerPrefs.Save();
                    }
                    _statusLog = $"[{DateTime.Now:HH:mm:ss}] Revoked fallback for: {product.id}";
                }
            }

            // Buy via Store
            if (GUILayout.Button("Buy (Store)"))
            {
                helper.Purchase(product.id);
                _statusLog = $"[{DateTime.Now:HH:mm:ss}] Sent purchase request for: {product.id}";
            }

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void ClearAllFallbacks(IAPHelper helper)
        {
            if (helper.products == null) return;

            foreach (var p in helper.products)
            {
                if (!string.IsNullOrEmpty(p.playerPrefsFallbackKey))
                {
                    PlayerPrefs.DeleteKey(p.playerPrefsFallbackKey);
                }
            }
            PlayerPrefs.Save();
        }

        #endregion

        #region Factory Method

        /// <summary>
        /// Creates an IAPDebugOverlay GameObject dynamically at runtime if one does not already exist.
        /// </summary>
        public static IAPDebugOverlay CreateOverlay()
        {
            var existing = FindObjectOfType<IAPDebugOverlay>();
            if (existing != null)
                return existing;

            var go = new GameObject("IAPDebugOverlay", typeof(IAPDebugOverlay));
            return go.GetComponent<IAPDebugOverlay>();
        }

        #endregion
    }
}

/// <summary>
/// Global namespace alias for convenience in inspector and prefab references.
/// </summary>
public class IAPDebugOverlay : Wagenheimer.IAPHelper.UI.IAPDebugOverlay
{
}
