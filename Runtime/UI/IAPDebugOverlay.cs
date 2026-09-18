using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Purchasing;

namespace Wagenheimer.IAPHelper.UI
{
    /// <summary>
    /// In-game runtime debug overlay for testing multi-product IAP flows.
    /// Provides real-time store inspection, entitlement state, simulated revoke/restore triggers,
    /// and an event log in Development Builds and Unity Editor.
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
        private Rect _windowRect = new Rect(10, 10, 520, 580);
        private Vector2 _scrollPos;

        // Event log
        private readonly List<string> _eventLog = new List<string>();
        private const int MaxLogLines = 12;

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

        private void OnEnable()
        {
            if (IAPHelper.Instance != null)
            {
                IAPHelper.Instance.OnEntitlementGranted   += OnGranted;
                IAPHelper.Instance.OnEntitlementRevoked   += OnRevoked;
                IAPHelper.Instance.OnPurchasesFetched     += OnRestored;
                IAPHelper.Instance.OnConnectionFailed     += OnConnFailed;
            }
        }

        private void OnDisable()
        {
            if (IAPHelper.Instance != null)
            {
                IAPHelper.Instance.OnEntitlementGranted   -= OnGranted;
                IAPHelper.Instance.OnEntitlementRevoked   -= OnRevoked;
                IAPHelper.Instance.OnPurchasesFetched     -= OnRestored;
                IAPHelper.Instance.OnConnectionFailed     -= OnConnFailed;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                _isOpen = !_isOpen;
        }

        private void OnGUI()
        {
            if (!Debug.isDebugBuild && !Application.isEditor && !enableInReleaseBuilds)
                return;

            GUI.depth = -9999;

            if (showFloatingButton && !_isOpen)
            {
                if (GUI.Button(new Rect(5, Screen.height - 40, 90, 30), "IAP DBG"))
                    _isOpen = true;
            }

            if (_isOpen)
            {
                _windowRect = GUI.Window(888123, _windowRect, DrawDebugWindow, "IAP Helper — Debug Panel");
            }
        }

        #endregion

        #region Event Handlers

        private void OnGranted(string id)       => Log($"<color=lime>GRANTED</color>: {id}");
        private void OnRevoked(string id)       => Log($"<color=red>REVOKED</color>: {id}");
        private void OnRestored(Orders _)       => Log($"<color=cyan>FETCH PURCHASES</color> completed");
        private void OnConnFailed(string msg)   => Log($"<color=red>CONN FAILED</color>: {msg}");

        private void Log(string msg)
        {
            _eventLog.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            if (_eventLog.Count > MaxLogLines)
                _eventLog.RemoveAt(0);
        }

        #endregion

        #region GUI Layout

        private void DrawDebugWindow(int windowId)
        {
            GUI.DragWindow(new Rect(0, 0, _windowRect.width - 60, 22));

            if (GUI.Button(new Rect(_windowRect.width - 55, 3, 50, 18), "Close"))
            {
                _isOpen = false;
                return;
            }

            GUILayout.Space(24);

            var helper = IAPHelper.Instance;
            if (helper == null)
            {
                GUILayout.Label("<color=red><b>IAPHelper.Instance is null — not initialized yet.</b></color>");
                return;
            }

            DrawConnectionBanner(helper);
            GUILayout.Space(4);
            DrawGlobalActions(helper);
            GUILayout.Space(4);
            DrawProductsCatalog(helper);
            GUILayout.Space(4);
            DrawEventLog();
        }

        // ── Connection Banner ─────────────────────────────────────────────────────

        private void DrawConnectionBanner(IAPHelper helper)
        {
            GUILayout.BeginVertical("box");

            string connColor = helper.IsConnected ? "lime" : "yellow";
            string connLabel = helper.IsConnected ? "Connected" : "Disconnected / Initializing";
            GUILayout.Label($"<b>Store:</b> <color={connColor}>{connLabel}</color>  " +
                            $"<b>Products:</b> {(helper.ProductsLoaded ? "<color=lime>OK</color>" : "<color=yellow>Pending</color>")}  " +
                            $"<b>Auto-Restore:</b> {helper.autoRestorePurchases}");

            GUILayout.Label($"<b>Platform:</b> {Application.platform}  " +
                            $"<b>HasPurchasedFallback:</b> " +
                            $"{(IAPHelper.HasPurchasedFallback != null ? "<color=lime>wired</color>" : "<color=yellow>null — store + PlayerPrefs only</color>")}");

            GUILayout.EndVertical();
        }

        // ── Global Actions ────────────────────────────────────────────────────────

        private void DrawGlobalActions(IAPHelper helper)
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Force Init"))
            {
                helper.Initialize();
                Log("Initialize() called.");
            }

            if (GUILayout.Button("Fetch / Restore All"))
            {
                helper.FetchPurchases();
                Log("FetchPurchases() called.");
            }

            if (GUILayout.Button("Clear All PlayerPrefs Keys"))
            {
                ClearAllFallbacks(helper);
                Log("All PlayerPrefs fallback keys cleared.");
            }

            GUILayout.EndHorizontal();
        }

        // ── Products ──────────────────────────────────────────────────────────────

        private void DrawProductsCatalog(IAPHelper helper)
        {
            GUILayout.Label("<b>Products:</b>");

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(260));

            if (helper.products == null || helper.products.Count == 0)
            {
                GUILayout.Label("No products configured.");
            }
            else
            {
                foreach (var product in helper.products)
                    DrawProductRow(helper, product);
            }

            GUILayout.EndScrollView();
        }

        private void DrawProductRow(IAPHelper helper, ProductConfig product)
        {
            GUILayout.BeginVertical("box");

            // ── Header row ────────────────────────────────────────────────────────
            bool storeOwned      = false;
            bool fallbackOwned   = IAPHelper.HasPurchasedFallback != null && IAPHelper.HasPurchasedFallback(product.id);
            bool prefOwned       = !string.IsNullOrEmpty(product.playerPrefsFallbackKey) &&
                                   PlayerPrefs.GetInt(product.playerPrefsFallbackKey, 0) == 1;

            // Store ownership check (requires connection)
            if (helper.IsConnected && helper.ProductsLoaded)
                storeOwned = helper.HasPurchased(product.id) && !fallbackOwned && !prefOwned;

            bool isOwned = helper.HasPurchased(product.id);
            string ownColor = isOwned ? "lime" : "white";
            string price    = helper.GetPrice(product.id);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b><color=cyan>{product.id}</color></b> ({product.type})");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{(!string.IsNullOrEmpty(price) ? price : "(loading)")}");
            GUILayout.Label($"  Owned: <color={ownColor}><b>{(isOwned ? "YES" : "NO")}</b></color>");
            GUILayout.EndHorizontal();

            // Ownership source breakdown
            GUILayout.BeginHorizontal();
            GUILayout.Label($"  <color=grey>Store: {(storeOwned ? "✓" : "—")}  " +
                            $"PlayerPrefs: {(prefOwned ? "✓ (" + product.playerPrefsFallbackKey + ")" : "—")}  " +
                            $"Fallback: {(fallbackOwned ? "✓" : "—")}</color>",
                            GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            // ── Action buttons ────────────────────────────────────────────────────
            GUILayout.BeginHorizontal();

            // Grant: fire OnEntitlementGranted + UnityEvent
            if (GUILayout.Button("Simulate Grant"))
            {
                if (!string.IsNullOrEmpty(product.playerPrefsFallbackKey))
                {
                    PlayerPrefs.SetInt(product.playerPrefsFallbackKey, 1);
                    PlayerPrefs.Save();
                }
                product.onEntitlementGranted?.Invoke();
                Log($"Simulated grant: {product.id}");
            }

            if (product.type == ProductType.NonConsumable)
            {
                // Revoke only: clears local cache + fires OnEntitlementRevoked
                if (GUILayout.Button("Revoke Local"))
                {
                    helper.DebugRevokeEntitlement(product.id);
                    Log($"DEBUG Revoke: {product.id}");
                }

                // Revoke + immediately trigger FetchPurchases to test full restore loop
                GUI.backgroundColor = new Color(1f, 0.6f, 0.1f);
                if (GUILayout.Button("Revoke & Restore"))
                {
                    helper.DebugResetAndRestore(product.id);
                    Log($"DEBUG Revoke+Restore: {product.id}");
                }
                GUI.backgroundColor = Color.white;
            }

            // Real purchase via store
            GUI.backgroundColor = new Color(0.3f, 1f, 0.3f);
            if (GUILayout.Button("Buy"))
            {
                helper.Purchase(product.id);
                Log($"Purchase sent: {product.id}");
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        // ── Event Log ─────────────────────────────────────────────────────────────

        private void DrawEventLog()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Event Log:</b>");

            if (_eventLog.Count == 0)
            {
                GUILayout.Label("<color=grey>No events yet.</color>");
            }
            else
            {
                for (int i = _eventLog.Count - 1; i >= 0; i--)
                    GUILayout.Label(_eventLog[i]);
            }

            GUILayout.EndVertical();
        }

        private void ClearAllFallbacks(IAPHelper helper)
        {
            if (helper.products == null) return;

            foreach (var p in helper.products)
            {
                if (!string.IsNullOrEmpty(p.playerPrefsFallbackKey))
                    PlayerPrefs.DeleteKey(p.playerPrefsFallbackKey);
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
            if (existing != null) return existing;

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
