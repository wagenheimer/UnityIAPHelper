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

        #region Palette

        private static readonly Color ColorBackground = new Color(0.106f, 0.114f, 0.157f, 0.97f);
        private static readonly Color ColorPanel = new Color(0.161f, 0.173f, 0.227f, 1f);
        private static readonly Color ColorHeaderFrom = new Color(0.373f, 0.204f, 0.804f, 1f);
        private static readonly Color ColorHeaderTo = new Color(0.204f, 0.463f, 0.902f, 1f);
        private static readonly Color ColorAccentGreen = new Color(0.298f, 0.851f, 0.392f, 1f);
        private static readonly Color ColorAccentAmber = new Color(0.984f, 0.749f, 0.141f, 1f);
        private static readonly Color ColorAccentRed = new Color(0.937f, 0.325f, 0.314f, 1f);
        private static readonly Color ColorAccentCyan = new Color(0.278f, 0.827f, 0.902f, 1f);
        private static readonly Color ColorAccentOrange = new Color(1f, 0.596f, 0.145f, 1f);
        private static readonly Color ColorTextMuted = new Color(0.62f, 0.65f, 0.71f, 1f);

        #endregion

        #region Private Fields

        private bool _isOpen;
        private Rect _windowRect = new Rect(10, 10, 560, 620);
        private Vector2 _scrollPos;

        // Event log
        private readonly List<(string text, Color color)> _eventLog = new List<(string, Color)>();
        private const int MaxLogLines = 14;

        // Lazily-built GUI skin (must be created inside OnGUI)
        private bool _skinReady;
        private GUIStyle _windowStyle;
        private GUIStyle _headerLabelStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _bodyLabelStyle;
        private GUIStyle _mutedLabelStyle;
        private GUIStyle _pillStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _closeButtonStyle;
        private GUIStyle _floatingButtonStyle;
        private readonly Dictionary<Color, Texture2D> _textureCache = new Dictionary<Color, Texture2D>();

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

        private void OnDestroy()
        {
            foreach (var tex in _textureCache.Values)
                if (tex != null) Destroy(tex);
            _textureCache.Clear();
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

            EnsureSkin();

            GUI.depth = -9999;

            if (showFloatingButton && !_isOpen)
            {
                if (GUI.Button(new Rect(5, Screen.height - 46, 108, 34), "🛒 IAP DBG", _floatingButtonStyle))
                    _isOpen = true;
            }

            if (_isOpen)
            {
                _windowRect = GUI.Window(888123, _windowRect, DrawDebugWindow, GUIContent.none, _windowStyle);
            }
        }

        #endregion

        #region Event Handlers

        private void OnGranted(string id)       => Log($"GRANTED: {id}", ColorAccentGreen);
        private void OnRevoked(string id)       => Log($"REVOKED: {id}", ColorAccentRed);
        private void OnRestored(Orders _)       => Log("FETCH PURCHASES completed", ColorAccentCyan);
        private void OnConnFailed(string msg)   => Log($"CONN FAILED: {msg}", ColorAccentRed);

        private void Log(string msg, Color color)
        {
            _eventLog.Add(($"[{DateTime.Now:HH:mm:ss}] {msg}", color));
            if (_eventLog.Count > MaxLogLines)
                _eventLog.RemoveAt(0);
        }

        #endregion

        #region GUI Skin

        private Texture2D SolidTexture(Color color)
        {
            if (_textureCache.TryGetValue(color, out var cached) && cached != null)
                return cached;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            _textureCache[color] = tex;
            return tex;
        }

        private Texture2D GradientTexture(Color from, Color to, int width = 64)
        {
            var key = from * 1000f + to;
            if (_textureCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
            for (int x = 0; x < width; x++)
                tex.SetPixel(x, 0, Color.Lerp(from, to, x / (float)(width - 1)));
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            _textureCache[key] = tex;
            return tex;
        }

        private void EnsureSkin()
        {
            if (_skinReady) return;
            _skinReady = true;

            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                normal = { background = SolidTexture(ColorBackground) },
                onNormal = { background = SolidTexture(ColorBackground) },
                padding = new RectOffset(10, 10, 26, 10),
                border = new RectOffset(6, 6, 22, 6)
            };

            _headerLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4)
            };

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = SolidTexture(ColorPanel) },
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 4, 6)
            };

            _sectionTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = ColorAccentCyan },
                margin = new RectOffset(2, 2, 2, 4)
            };

            _bodyLabelStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 12,
                normal = { textColor = Color.white },
                wordWrap = true
            };

            _mutedLabelStyle = new GUIStyle(_bodyLabelStyle)
            {
                normal = { textColor = ColorTextMuted },
                fontSize = 11
            };

            _pillStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 3, 3)
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(8, 8, 6, 6)
            };

            _closeButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white, background = SolidTexture(ColorAccentRed) },
                hover = { textColor = Color.white, background = SolidTexture(ColorAccentRed) }
            };

            _floatingButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void DrawPill(string text, Color color)
        {
            var content = new GUIContent(text);
            var size = _pillStyle.CalcSize(content);
            var rect = GUILayoutUtility.GetRect(size.x, size.y);
            GUI.DrawTexture(rect, SolidTexture(new Color(color.r, color.g, color.b, 0.22f)));
            GUI.Label(rect, text, _pillStyle);
        }

        #endregion

        #region GUI Layout

        private void DrawDebugWindow(int windowId)
        {
            // ── Colorful header bar ──────────────────────────────────────────────
            var headerRect = new Rect(0, 0, _windowRect.width, 22);
            GUI.DrawTexture(headerRect, GradientTexture(ColorHeaderFrom, ColorHeaderTo, 128));
            GUI.Label(new Rect(10, 0, _windowRect.width - 80, 22), "🛒 IAP Helper — Debug Panel", _headerLabelStyle);

            if (GUI.Button(new Rect(_windowRect.width - 62, 2, 56, 18), "Close ✕", _closeButtonStyle))
            {
                _isOpen = false;
                return;
            }

            GUI.DragWindow(new Rect(0, 0, _windowRect.width - 66, 22));

            GUILayout.Space(6);

            var helper = IAPHelper.Instance;
            if (helper == null)
            {
                GUILayout.BeginVertical(_panelStyle);
                GUILayout.Label("<b><color=#EF5350>IAPHelper.Instance is null — not initialized yet.</color></b>", _bodyLabelStyle);
                GUILayout.EndVertical();
                return;
            }

            DrawConnectionBanner(helper);
            DrawGlobalActions(helper);
            DrawProductsCatalog(helper);
            DrawEventLog();
        }

        // ── Connection Banner ─────────────────────────────────────────────────────

        private void DrawConnectionBanner(IAPHelper helper)
        {
            GUILayout.BeginVertical(_panelStyle);
            GUILayout.Label("STATUS", _sectionTitleStyle);

            GUILayout.BeginHorizontal();
            DrawPill(helper.IsConnected ? "● CONNECTED" : "● DISCONNECTED", helper.IsConnected ? ColorAccentGreen : ColorAccentAmber);
            GUILayout.Space(6);
            DrawPill(helper.ProductsLoaded ? "PRODUCTS OK" : "PRODUCTS PENDING", helper.ProductsLoaded ? ColorAccentGreen : ColorAccentAmber);
            GUILayout.Space(6);
            DrawPill(helper.autoRestorePurchases ? "AUTO-RESTORE ON" : "AUTO-RESTORE OFF", helper.autoRestorePurchases ? ColorAccentCyan : ColorTextMuted);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label($"<b>Platform:</b> {Application.platform}   " +
                            $"<b>HasPurchasedFallback:</b> " +
                            $"{(IAPHelper.HasPurchasedFallback != null ? "<color=#4CD964>wired</color>" : "<color=#FBBF24>store + PlayerPrefs only</color>")}",
                            _bodyLabelStyle);

            GUILayout.EndVertical();
        }

        // ── Global Actions ────────────────────────────────────────────────────────

        private void DrawGlobalActions(IAPHelper helper)
        {
            GUILayout.BeginVertical(_panelStyle);
            GUILayout.Label("ACTIONS", _sectionTitleStyle);

            GUILayout.BeginHorizontal();

            GUI.backgroundColor = ColorAccentCyan;
            if (GUILayout.Button("⟳ Force Init", _buttonStyle))
            {
                helper.Initialize();
                Log("Initialize() called.", ColorAccentCyan);
            }

            GUI.backgroundColor = ColorHeaderTo;
            if (GUILayout.Button("⇩ Fetch / Restore All", _buttonStyle))
            {
                helper.FetchPurchases();
                Log("FetchPurchases() called.", ColorHeaderTo);
            }

            GUI.backgroundColor = ColorAccentOrange;
            if (GUILayout.Button("🗑 Clear All PlayerPrefs Keys", _buttonStyle))
            {
                ClearAllFallbacks(helper);
                Log("All PlayerPrefs fallback keys cleared.", ColorAccentOrange);
            }

            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        // ── Products ──────────────────────────────────────────────────────────────

        private void DrawProductsCatalog(IAPHelper helper)
        {
            GUILayout.BeginVertical(_panelStyle);
            GUILayout.Label("PRODUCTS", _sectionTitleStyle);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(230));

            if (helper.products == null || helper.products.Count == 0)
            {
                GUILayout.Label("No products configured.", _mutedLabelStyle);
            }
            else
            {
                foreach (var product in helper.products)
                    DrawProductRow(helper, product);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawProductRow(IAPHelper helper, ProductConfig product)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            // ── Header row ────────────────────────────────────────────────────────
            bool storeOwned      = false;
            bool fallbackOwned   = IAPHelper.HasPurchasedFallback != null && IAPHelper.HasPurchasedFallback(product.id);
            bool prefOwned       = !string.IsNullOrEmpty(product.playerPrefsFallbackKey) &&
                                   PlayerPrefs.GetInt(product.playerPrefsFallbackKey, 0) == 1;

            // Store ownership check (requires connection)
            if (helper.IsConnected && helper.ProductsLoaded)
                storeOwned = helper.HasPurchased(product.id) && !fallbackOwned && !prefOwned;

            bool isOwned = helper.HasPurchased(product.id);
            string price = helper.GetPrice(product.id);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b><color=#4FC3F7>{product.id}</color></b> <color=#9AA5B1>({product.type})</color>", _bodyLabelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(!string.IsNullOrEmpty(price) ? price : "(loading)", _mutedLabelStyle);
            GUILayout.Space(6);
            DrawPill(isOwned ? "OWNED" : "NOT OWNED", isOwned ? ColorAccentGreen : ColorTextMuted);
            GUILayout.EndHorizontal();

            // Ownership source breakdown
            GUILayout.Label($"Store: {(storeOwned ? "✓" : "—")}   " +
                            $"PlayerPrefs: {(prefOwned ? "✓ (" + product.playerPrefsFallbackKey + ")" : "—")}   " +
                            $"Fallback: {(fallbackOwned ? "✓" : "—")}",
                            _mutedLabelStyle);

            // ── Action buttons ────────────────────────────────────────────────────
            GUILayout.BeginHorizontal();

            // Grant: fire OnEntitlementGranted + UnityEvent
            GUI.backgroundColor = ColorAccentGreen;
            if (GUILayout.Button("✓ Simulate Grant", _buttonStyle))
            {
                if (!string.IsNullOrEmpty(product.playerPrefsFallbackKey))
                {
                    PlayerPrefs.SetInt(product.playerPrefsFallbackKey, 1);
                    PlayerPrefs.Save();
                }
                product.onEntitlementGranted?.Invoke();
                Log($"Simulated grant: {product.id}", ColorAccentGreen);
            }

            if (product.type == ProductType.NonConsumable)
            {
                // Revoke only: clears local cache + fires OnEntitlementRevoked
                GUI.backgroundColor = ColorAccentRed;
                if (GUILayout.Button("✕ Revoke Local", _buttonStyle))
                {
                    helper.DebugRevokeEntitlement(product.id);
                    Log($"DEBUG Revoke: {product.id}", ColorAccentRed);
                }

                // Revoke + immediately trigger FetchPurchases to test full restore loop
                GUI.backgroundColor = ColorAccentOrange;
                if (GUILayout.Button("↻ Revoke & Restore", _buttonStyle))
                {
                    helper.DebugResetAndRestore(product.id);
                    Log($"DEBUG Revoke+Restore: {product.id}", ColorAccentOrange);
                }
            }

            // Real purchase via store
            GUI.backgroundColor = ColorHeaderTo;
            if (GUILayout.Button("💳 Buy", _buttonStyle))
            {
                helper.Purchase(product.id);
                Log($"Purchase sent: {product.id}", ColorHeaderTo);
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        // ── Event Log ─────────────────────────────────────────────────────────────

        private void DrawEventLog()
        {
            GUILayout.BeginVertical(_panelStyle);
            GUILayout.Label("EVENT LOG", _sectionTitleStyle);

            if (_eventLog.Count == 0)
            {
                GUILayout.Label("No events yet.", _mutedLabelStyle);
            }
            else
            {
                for (int i = _eventLog.Count - 1; i >= 0; i--)
                {
                    var (text, color) = _eventLog[i];
                    var hex = ColorUtility.ToHtmlStringRGB(color);
                    GUILayout.Label($"<color=#{hex}>{text}</color>", _bodyLabelStyle);
                }
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
