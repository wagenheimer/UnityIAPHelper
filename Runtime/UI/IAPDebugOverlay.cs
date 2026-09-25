using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UIElements;

namespace Wagenheimer.IAPHelper.UI
{
    /// <summary>
    /// In-game runtime UI Toolkit debug overlay for testing multi-product IAP flows.
    /// Provides real-time store inspection, entitlement state, simulated revoke/restore triggers,
    /// and a live event log in Development Builds and Unity Editor.
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

        [Header("Scale (mobile-friendly)")]
        [Tooltip("UI scale used automatically on touch platforms.")]
        [Range(1f, 3f)]
        public float mobileDefaultScale = 1.75f;

        [Tooltip("UI scale used on desktop/Editor.")]
        [Range(0.75f, 3f)]
        public float desktopDefaultScale = 1f;

        [Tooltip("Optional custom PanelSettings. If null, a high-priority runtime PanelSettings is created automatically.")]
        public PanelSettings customPanelSettings;

        #endregion

        #region Private Fields

        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _floatingBtn;
        private VisualElement _floatingDot;
        private VisualElement _window;
        private ScrollView _scrollView;

        private VisualElement _statusCard;
        private VisualElement _productsContainer;
        private VisualElement _eventLogContainer;

        private bool _isOpen;
        private float _lastRefreshTime;
        private const float RefreshInterval = 0.4f;

        private readonly List<(string text, Color color)> _eventLog = new List<(string, Color)>();
        private const int MaxLogLines = 16;

        // Window drag state
        private bool _isDragging;
        private Vector2 _dragStartPointer;
        private Vector2 _dragStartWindowPos;

        // Floating button drag state
        private bool _isFloatingDragging;
        private Vector2 _floatingDragStartPointer;
        private Vector2 _floatingDragStartPos;
        private bool _hasDraggedFloating;

        #endregion

        #region Palette

        private static readonly Color ColorAccentGreen  = new Color(0.18f, 0.75f, 0.38f);
        private static readonly Color ColorAccentAmber  = new Color(0.95f, 0.70f, 0.18f);
        private static readonly Color ColorAccentRed    = new Color(0.90f, 0.30f, 0.28f);
        private static readonly Color ColorAccentCyan   = new Color(0.28f, 0.78f, 0.95f);
        private static readonly Color ColorAccentOrange = new Color(0.98f, 0.55f, 0.18f);
        private static readonly Color ColorTextMuted    = new Color(0.65f, 0.68f, 0.75f);

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (!Debug.isDebugBuild && !Application.isEditor && !enableInReleaseBuilds)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            InitializeUI();
        }

        private void OnEnable()
        {
            if (IAPHelper.Instance != null)
            {
                IAPHelper.Instance.OnEntitlementGranted += OnGranted;
                IAPHelper.Instance.OnEntitlementRevoked += OnRevoked;
                IAPHelper.Instance.OnPurchasesFetched   += OnRestored;
                IAPHelper.Instance.OnConnectionFailed   += OnConnFailed;
            }
        }

        private void OnDisable()
        {
            if (IAPHelper.Instance != null)
            {
                IAPHelper.Instance.OnEntitlementGranted -= OnGranted;
                IAPHelper.Instance.OnEntitlementRevoked -= OnRevoked;
                IAPHelper.Instance.OnPurchasesFetched   -= OnRestored;
                IAPHelper.Instance.OnConnectionFailed   -= OnConnFailed;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                SetOpen(!_isOpen);
            }

            if (_isOpen && Time.unscaledTime - _lastRefreshTime >= RefreshInterval)
            {
                _lastRefreshTime = Time.unscaledTime;
                RefreshData();
            }
        }

        #endregion

        #region Event Handlers

        private void OnGranted(string id)     => Log($"GRANTED: {id}", ColorAccentGreen);
        private void OnRevoked(string id)     => Log($"REVOKED: {id}", ColorAccentRed);
        private void OnRestored(Orders _)     => Log("FETCH PURCHASES completed", ColorAccentCyan);
        private void OnConnFailed(string msg) => Log($"CONN FAILED: {msg}", ColorAccentRed);

        private void Log(string msg, Color color)
        {
            _eventLog.Insert(0, ($"[{DateTime.Now:HH:mm:ss}] {msg}", color));
            if (_eventLog.Count > MaxLogLines)
            {
                _eventLog.RemoveAt(_eventLog.Count - 1);
            }
            RefreshEventLog();
        }

        #endregion

        #region UI Toolkit Initialization

        private void InitializeUI()
        {
            _uiDocument = gameObject.GetComponent<UIDocument>();
            if (_uiDocument == null)
            {
                _uiDocument = gameObject.AddComponent<UIDocument>();
            }

            EnsurePanelSettings();

            _root = _uiDocument.rootVisualElement;
            _root.Clear();
            _root.pickingMode = PickingMode.Ignore;

            BuildFloatingButton();
            BuildWindow();

            SetOpen(false);
            RefreshData();
        }

        private void EnsurePanelSettings()
        {
            if (_uiDocument.panelSettings != null) return;

            if (customPanelSettings != null)
            {
                _uiDocument.panelSettings = customPanelSettings;
                return;
            }

            // Project-wide override first, then the PanelSettings+theme shipped with this package.
            // The shipped asset is required: in player builds no ThemeStyleSheet is loaded, so a
            // runtime-created PanelSettings would render nothing.
            var loaded = Resources.Load<PanelSettings>("Wagenheimer/DebugPanelSettings")
                         ?? Resources.Load<PanelSettings>("Wagenheimer/IAPDebugPanelSettings");
            if (loaded != null)
            {
                _uiDocument.panelSettings = loaded;
                return;
            }

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "IAPDebugPanelSettings";
            ps.sortingOrder = 9999;
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.match = 0.5f;

            var themes = Resources.FindObjectsOfTypeAll<ThemeStyleSheet>();
            if (themes != null && themes.Length > 0)
            {
                ps.themeStyleSheet = themes[0];
            }

            _uiDocument.panelSettings = ps;
        }

        #endregion

        #region Floating Button

        private void BuildFloatingButton()
        {
            _floatingBtn = new VisualElement();
            _floatingBtn.name = "IAPDebugFloatingButton";
            _floatingBtn.pickingMode = PickingMode.Position;
            _floatingBtn.style.position = Position.Absolute;
            _floatingBtn.style.bottom = 18;
            _floatingBtn.style.left = 18;
            _floatingBtn.style.height = 34;
            _floatingBtn.style.paddingLeft = 12;
            _floatingBtn.style.paddingRight = 12;
            _floatingBtn.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.15f, 0.94f));
            _floatingBtn.style.borderTopWidth = 1;
            _floatingBtn.style.borderBottomWidth = 1;
            _floatingBtn.style.borderLeftWidth = 1;
            _floatingBtn.style.borderRightWidth = 1;
            _floatingBtn.style.borderTopColor = new StyleColor(new Color(0.38f, 0.45f, 0.95f, 0.8f));
            _floatingBtn.style.borderBottomColor = new StyleColor(new Color(0.38f, 0.45f, 0.95f, 0.8f));
            _floatingBtn.style.borderLeftColor = new StyleColor(new Color(0.38f, 0.45f, 0.95f, 0.8f));
            _floatingBtn.style.borderRightColor = new StyleColor(new Color(0.38f, 0.45f, 0.95f, 0.8f));
            _floatingBtn.style.borderTopLeftRadius = 17;
            _floatingBtn.style.borderTopRightRadius = 17;
            _floatingBtn.style.borderBottomLeftRadius = 17;
            _floatingBtn.style.borderBottomRightRadius = 17;
            _floatingBtn.style.flexDirection = FlexDirection.Row;
            _floatingBtn.style.alignItems = Align.Center;
            _floatingBtn.style.justifyContent = Justify.Center;

            _floatingDot = new VisualElement();
            _floatingDot.style.width = 8;
            _floatingDot.style.height = 8;
            _floatingDot.style.borderTopLeftRadius = 4;
            _floatingDot.style.borderTopRightRadius = 4;
            _floatingDot.style.borderBottomLeftRadius = 4;
            _floatingDot.style.borderBottomRightRadius = 4;
            _floatingDot.style.backgroundColor = new StyleColor(ColorAccentAmber);
            _floatingDot.style.marginRight = 6;
            _floatingBtn.Add(_floatingDot);

            var label = new Label("🛒 IAP DBG");
            label.style.color = new StyleColor(Color.white);
            label.style.fontSize = 11.5f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _floatingBtn.Add(label);

            // Drag / Click handling
            _floatingBtn.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _isFloatingDragging = true;
                _hasDraggedFloating = false;
                _floatingDragStartPointer = evt.position;
                _floatingDragStartPos = new Vector2(_floatingBtn.resolvedStyle.left, _floatingBtn.resolvedStyle.top);
                _floatingBtn.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            _floatingBtn.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_isFloatingDragging) return;
                Vector2 delta = (Vector2)evt.position - _floatingDragStartPointer;
                if (delta.sqrMagnitude > 16f) _hasDraggedFloating = true;

                if (_hasDraggedFloating)
                {
                    _floatingBtn.style.bottom = StyleKeyword.Auto;
                    _floatingBtn.style.left = Mathf.Max(0, _floatingDragStartPos.x + delta.x);
                    _floatingBtn.style.top = Mathf.Max(0, _floatingDragStartPos.y + delta.y);
                }
                evt.StopPropagation();
            });

            _floatingBtn.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_isFloatingDragging) return;
                _isFloatingDragging = false;
                _floatingBtn.ReleasePointer(evt.pointerId);
                evt.StopPropagation();

                if (!_hasDraggedFloating)
                {
                    SetOpen(true);
                }
            });

            _root.Add(_floatingBtn);
        }

        #endregion

        #region Main Window

        private void BuildWindow()
        {
            _window = new VisualElement();
            _window.name = "IAPDebugWindow";
            _window.pickingMode = PickingMode.Position;
            _window.style.position = Position.Absolute;
            _window.style.left = 24;
            _window.style.top = 28;
            _window.style.width = 500;
            _window.style.maxHeight = new StyleLength(new Length(88, LengthUnit.Percent));
            _window.style.backgroundColor = new StyleColor(new Color(0.09f, 0.09f, 0.12f, 0.97f));
            _window.style.borderTopWidth = 1;
            _window.style.borderBottomWidth = 1;
            _window.style.borderLeftWidth = 1;
            _window.style.borderRightWidth = 1;
            _window.style.borderTopColor = new StyleColor(new Color(0.25f, 0.26f, 0.34f));
            _window.style.borderBottomColor = new StyleColor(new Color(0.25f, 0.26f, 0.34f));
            _window.style.borderLeftColor = new StyleColor(new Color(0.25f, 0.26f, 0.34f));
            _window.style.borderRightColor = new StyleColor(new Color(0.25f, 0.26f, 0.34f));
            _window.style.borderTopLeftRadius = 10;
            _window.style.borderTopRightRadius = 10;
            _window.style.borderBottomLeftRadius = 10;
            _window.style.borderBottomRightRadius = 10;
            _window.style.overflow = Overflow.Hidden;

            // Header (Draggable)
            var header = BuildHeader();
            _window.Add(header);

            // Scrollable Content
            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.style.flexGrow = 1;
            _scrollView.style.paddingLeft = 12;
            _scrollView.style.paddingRight = 12;
            _scrollView.style.paddingTop = 10;
            _scrollView.style.paddingBottom = 12;

            _statusCard = BuildStatusSection();
            _scrollView.Add(_statusCard);
            _scrollView.Add(BuildGlobalActionsSection());

            _productsContainer = new VisualElement();
            _scrollView.Add(_productsContainer);

            _scrollView.Add(BuildEventLogSection());

            _window.Add(_scrollView);
            _root.Add(_window);
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.height = 38;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 8;
            header.style.backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.20f));
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.22f, 0.23f, 0.30f));

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var titleLbl = new Label("🛒 IAP Helper Debug");
            titleLbl.style.fontSize = 13;
            titleLbl.style.color = new StyleColor(Color.white);
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleRow.Add(titleLbl);

            var liveBadge = CreatePill("LIVE", new Color(0.18f, 0.45f, 0.85f), Color.white);
            liveBadge.style.marginLeft = 8;
            titleRow.Add(liveBadge);

            header.Add(titleRow);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.alignItems = Align.Center;

            var minBtn = CreateSmallButton("—", () => SetOpen(false));
            minBtn.style.marginRight = 4;
            actions.Add(minBtn);

            var closeBtn = CreateSmallButton("✕", () => SetOpen(false));
            actions.Add(closeBtn);

            header.Add(actions);

            // Drag handling
            header.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _isDragging = true;
                _dragStartPointer = evt.position;
                _dragStartWindowPos = new Vector2(_window.resolvedStyle.left, _window.resolvedStyle.top);
                header.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            header.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_isDragging) return;
                Vector2 delta = (Vector2)evt.position - _dragStartPointer;
                _window.style.left = Mathf.Max(0, _dragStartWindowPos.x + delta.x);
                _window.style.top = Mathf.Max(0, _dragStartWindowPos.y + delta.y);
                evt.StopPropagation();
            });

            header.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_isDragging) return;
                _isDragging = false;
                header.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });

            return header;
        }

        #endregion

        #region Content Sections

        private VisualElement BuildStatusSection()
        {
            var card = CreateCard("Store & System Status");

            var helper = IAPHelper.Instance;
            bool connected = helper != null && helper.IsConnected;
            bool loaded = helper != null && helper.ProductsLoaded;
            bool autoRestore = helper != null && helper.autoRestorePurchases;

            var badgesRow = new VisualElement();
            badgesRow.style.flexDirection = FlexDirection.Row;
            badgesRow.style.marginBottom = 6;

            var connPill = CreatePill(connected ? "● CONNECTED" : "● DISCONNECTED", connected ? ColorAccentGreen : ColorAccentAmber, Color.white);
            connPill.style.marginRight = 6;
            badgesRow.Add(connPill);

            var prodPill = CreatePill(loaded ? "PRODUCTS LOADED" : "PRODUCTS PENDING", loaded ? ColorAccentGreen : ColorAccentAmber, Color.white);
            prodPill.style.marginRight = 6;
            badgesRow.Add(prodPill);

            var autoPill = CreatePill(autoRestore ? "AUTO-RESTORE ON" : "AUTO-RESTORE OFF", autoRestore ? ColorAccentCyan : ColorTextMuted, Color.white);
            badgesRow.Add(autoPill);

            card.Add(badgesRow);

            CreateRow(card, "Platform", Application.platform.ToString());
            CreateRow(card, "Fallback Delegate", IAPHelper.HasPurchasedFallback != null ? "Wired (Active)" : "Store + PlayerPrefs Only");

            return card;
        }

        private VisualElement BuildGlobalActionsSection()
        {
            var card = CreateCard("Global QA Actions");

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginBottom = 6;

            var initBtn = CreateButton("⟳ Force Init", new Color(0.20f, 0.42f, 0.65f), Color.white, () =>
            {
                var h = IAPHelper.Instance;
                if (h != null)
                {
                    h.Initialize();
                    Log("Initialize() called.", ColorAccentCyan);
                }
            });
            initBtn.style.flexGrow = 1;
            initBtn.style.marginRight = 4;
            btnRow.Add(initBtn);

            var restoreBtn = CreateButton("⇩ Restore All", new Color(0.22f, 0.28f, 0.48f), Color.white, () =>
            {
                var h = IAPHelper.Instance;
                if (h != null)
                {
                    h.FetchPurchases();
                    Log("FetchPurchases() called.", ColorAccentCyan);
                }
            });
            restoreBtn.style.flexGrow = 1;
            restoreBtn.style.marginRight = 4;
            btnRow.Add(restoreBtn);

            var clearPrefsBtn = CreateButton("🗑 Clear Keys", new Color(0.60f, 0.35f, 0.15f), Color.white, () =>
            {
                var h = IAPHelper.Instance;
                if (h != null)
                {
                    ClearAllFallbacks(h);
                    Log("All PlayerPrefs fallback keys cleared.", ColorAccentOrange);
                    RefreshData();
                }
            });
            clearPrefsBtn.style.flexGrow = 1;
            btnRow.Add(clearPrefsBtn);

            card.Add(btnRow);

            var copyReportBtn = CreateButton("📋 Copy Diagnostics Report", new Color(0.20f, 0.21f, 0.26f), Color.white, () =>
            {
                string rep = GenerateReport();
                GUIUtility.systemCopyBuffer = rep;
                Log("Diagnostic report copied to clipboard.", ColorAccentCyan);
            });
            copyReportBtn.style.height = 24;
            card.Add(copyReportBtn);

            return card;
        }

        private void RebuildProductsCatalog()
        {
            if (_productsContainer == null) return;
            _productsContainer.Clear();

            var card = CreateCard("Configured Products Catalog");

            var helper = IAPHelper.Instance;
            if (helper == null || helper.products == null || helper.products.Count == 0)
            {
                var empty = new Label(helper == null ? "IAPHelper instance is null." : "No products configured.");
                empty.style.fontSize = 11;
                empty.style.color = new StyleColor(ColorTextMuted);
                card.Add(empty);
                _productsContainer.Add(card);
                return;
            }

            foreach (var product in helper.products)
            {
                card.Add(BuildProductRow(helper, product));
            }

            _productsContainer.Add(card);
        }

        private VisualElement BuildProductRow(IAPHelper helper, ProductConfig product)
        {
            var box = new VisualElement();
            box.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.11f));
            box.style.borderTopWidth = 1;
            box.style.borderBottomWidth = 1;
            box.style.borderLeftWidth = 1;
            box.style.borderRightWidth = 1;
            box.style.borderTopColor = new StyleColor(new Color(0.18f, 0.19f, 0.24f));
            box.style.borderBottomColor = new StyleColor(new Color(0.18f, 0.19f, 0.24f));
            box.style.borderLeftColor = new StyleColor(new Color(0.18f, 0.19f, 0.24f));
            box.style.borderRightColor = new StyleColor(new Color(0.18f, 0.19f, 0.24f));
            box.style.borderTopLeftRadius = 6;
            box.style.borderTopRightRadius = 6;
            box.style.borderBottomLeftRadius = 6;
            box.style.borderBottomRightRadius = 6;
            box.style.paddingTop = 6;
            box.style.paddingBottom = 6;
            box.style.paddingLeft = 8;
            box.style.paddingRight = 8;
            box.style.marginBottom = 6;

            bool fallbackOwned = IAPHelper.HasPurchasedFallback != null && IAPHelper.HasPurchasedFallback(product.id);
            bool prefOwned = !string.IsNullOrEmpty(product.playerPrefsFallbackKey) &&
                             PlayerPrefs.GetInt(product.playerPrefsFallbackKey, 0) == 1;

            bool isOwned = helper.HasPurchased(product.id);
            string price = helper.GetPrice(product.id);

            // Header line
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;

            var idRow = new VisualElement();
            idRow.style.flexDirection = FlexDirection.Row;
            idRow.style.alignItems = Align.Center;

            var idLbl = new Label(product.id);
            idLbl.style.fontSize = 11.5f;
            idLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            idLbl.style.color = new StyleColor(new Color(0.40f, 0.75f, 0.98f));
            idRow.Add(idLbl);

            var typePill = CreatePill(product.type.ToString(), new Color(0.20f, 0.22f, 0.28f), ColorTextMuted);
            typePill.style.marginLeft = 6;
            idRow.Add(typePill);

            headerRow.Add(idRow);

            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;

            var priceLbl = new Label(!string.IsNullOrEmpty(price) ? price : "-");
            priceLbl.style.fontSize = 11;
            priceLbl.style.color = new StyleColor(ColorTextMuted);
            priceLbl.style.marginRight = 6;
            statusRow.Add(priceLbl);

            var ownedPill = CreatePill(isOwned ? "OWNED" : "NOT OWNED", isOwned ? ColorAccentGreen : new Color(0.25f, 0.26f, 0.32f), Color.white);
            statusRow.Add(ownedPill);

            headerRow.Add(statusRow);
            box.Add(headerRow);

            // Sub info
            var subInfo = new Label($"Store: {(isOwned && !prefOwned && !fallbackOwned ? "✓" : "—")}  |  Prefs: {(prefOwned ? "✓" : "—")}  |  Fallback: {(fallbackOwned ? "✓" : "—")}");
            subInfo.style.fontSize = 10;
            subInfo.style.color = new StyleColor(new Color(0.55f, 0.58f, 0.65f));
            subInfo.style.marginTop = 2;
            subInfo.style.marginBottom = 6;
            box.Add(subInfo);

            // Actions row
            var actionsRow = new VisualElement();
            actionsRow.style.flexDirection = FlexDirection.Row;

            var grantBtn = CreateButton("✓ Simulate Grant", new Color(0.18f, 0.52f, 0.30f), Color.white, () =>
            {
                if (!string.IsNullOrEmpty(product.playerPrefsFallbackKey))
                {
                    PlayerPrefs.SetInt(product.playerPrefsFallbackKey, 1);
                    PlayerPrefs.Save();
                }
                product.onEntitlementGranted?.Invoke();
                Log($"Simulated grant: {product.id}", ColorAccentGreen);
                RefreshData();
            });
            grantBtn.style.flexGrow = 1;
            grantBtn.style.marginRight = 4;
            actionsRow.Add(grantBtn);

            if (product.type == ProductType.NonConsumable)
            {
                var revokeBtn = CreateButton("✕ Revoke", new Color(0.55f, 0.20f, 0.20f), Color.white, () =>
                {
                    helper.DebugRevokeEntitlement(product.id);
                    Log($"Revoked: {product.id}", ColorAccentRed);
                    RefreshData();
                });
                revokeBtn.style.flexGrow = 1;
                revokeBtn.style.marginRight = 4;
                actionsRow.Add(revokeBtn);

                var restoreLoopBtn = CreateButton("↻ Revoke & Restore", new Color(0.55f, 0.35f, 0.15f), Color.white, () =>
                {
                    helper.DebugResetAndRestore(product.id);
                    Log($"Revoke & Restore triggered: {product.id}", ColorAccentOrange);
                    RefreshData();
                });
                restoreLoopBtn.style.flexGrow = 1;
                restoreLoopBtn.style.marginRight = 4;
                actionsRow.Add(restoreLoopBtn);
            }

            var buyBtn = CreateButton("💳 Buy", new Color(0.18f, 0.38f, 0.65f), Color.white, () =>
            {
                helper.Purchase(product.id);
                Log($"Purchase initiated: {product.id}", ColorAccentCyan);
            });
            buyBtn.style.flexGrow = 1;
            actionsRow.Add(buyBtn);

            box.Add(actionsRow);
            return box;
        }

        private VisualElement BuildEventLogSection()
        {
            var card = CreateCard("Real-Time Event Log");

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 6;

            var titleLbl = new Label("Captured Purchases & Callbacks");
            titleLbl.style.fontSize = 11;
            titleLbl.style.color = new StyleColor(ColorTextMuted);
            headerRow.Add(titleLbl);

            var clearBtn = CreateSmallButton("Clear", () =>
            {
                _eventLog.Clear();
                RefreshEventLog();
            });
            headerRow.Add(clearBtn);
            card.Add(headerRow);

            _eventLogContainer = new VisualElement();
            _eventLogContainer.style.backgroundColor = new StyleColor(new Color(0.06f, 0.06f, 0.08f));
            _eventLogContainer.style.borderTopWidth = 1;
            _eventLogContainer.style.borderBottomWidth = 1;
            _eventLogContainer.style.borderLeftWidth = 1;
            _eventLogContainer.style.borderRightWidth = 1;
            _eventLogContainer.style.borderTopColor = new StyleColor(new Color(0.18f, 0.18f, 0.22f));
            _eventLogContainer.style.borderBottomColor = new StyleColor(new Color(0.18f, 0.18f, 0.22f));
            _eventLogContainer.style.borderLeftColor = new StyleColor(new Color(0.18f, 0.18f, 0.22f));
            _eventLogContainer.style.borderRightColor = new StyleColor(new Color(0.18f, 0.18f, 0.22f));
            _eventLogContainer.style.borderTopLeftRadius = 6;
            _eventLogContainer.style.borderTopRightRadius = 6;
            _eventLogContainer.style.borderBottomLeftRadius = 6;
            _eventLogContainer.style.borderBottomRightRadius = 6;
            _eventLogContainer.style.paddingTop = 6;
            _eventLogContainer.style.paddingBottom = 6;
            _eventLogContainer.style.paddingLeft = 8;
            _eventLogContainer.style.paddingRight = 8;
            _eventLogContainer.style.minHeight = 50;

            card.Add(_eventLogContainer);
            RefreshEventLog();

            return card;
        }

        #endregion

        #region Data Refresh

        private void RefreshData()
        {
            var helper = IAPHelper.Instance;
            bool connected = helper != null && helper.IsConnected;

            if (_floatingDot != null)
            {
                _floatingDot.style.backgroundColor = new StyleColor(connected ? ColorAccentGreen : ColorAccentAmber);
            }

            if (_statusCard != null && _scrollView != null)
            {
                int idx = _scrollView.IndexOf(_statusCard);
                if (idx >= 0)
                {
                    _statusCard.RemoveFromHierarchy();
                    _statusCard = BuildStatusSection();
                    _scrollView.Insert(idx, _statusCard);
                }
            }

            RebuildProductsCatalog();
        }

        private void RefreshEventLog()
        {
            if (_eventLogContainer == null) return;
            _eventLogContainer.Clear();

            if (_eventLog.Count == 0)
            {
                var empty = new Label("No events yet.");
                empty.style.fontSize = 10.5f;
                empty.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.55f));
                _eventLogContainer.Add(empty);
                return;
            }

            foreach (var (text, color) in _eventLog)
            {
                var lbl = new Label(text);
                lbl.style.fontSize = 10.5f;
                lbl.style.color = new StyleColor(color);
                lbl.style.marginBottom = 2;
                _eventLogContainer.Add(lbl);
            }
        }

        #endregion

        #region Helpers & UI Factories

        public void SetOpen(bool open)
        {
            _isOpen = open;
            if (_window != null)
            {
                _window.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_floatingBtn != null)
            {
                _floatingBtn.style.display = (showFloatingButton && !_isOpen) ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_isOpen)
            {
                RefreshData();
            }
        }

        private VisualElement CreateCard(string title)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(new Color(0.12f, 0.13f, 0.17f, 0.9f));
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = new StyleColor(new Color(0.20f, 0.21f, 0.28f));
            card.style.borderBottomColor = new StyleColor(new Color(0.20f, 0.21f, 0.28f));
            card.style.borderLeftColor = new StyleColor(new Color(0.20f, 0.21f, 0.28f));
            card.style.borderRightColor = new StyleColor(new Color(0.20f, 0.21f, 0.28f));
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.marginBottom = 8;

            var titleLbl = new Label(title);
            titleLbl.style.fontSize = 11.5f;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.color = new StyleColor(new Color(0.40f, 0.75f, 0.98f));
            titleLbl.style.marginBottom = 6;
            card.Add(titleLbl);

            return card;
        }

        private Label CreateRow(VisualElement parent, string key, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;

            var keyLbl = new Label(key);
            keyLbl.style.fontSize = 11;
            keyLbl.style.color = new StyleColor(new Color(0.68f, 0.70f, 0.76f));
            row.Add(keyLbl);

            var valLbl = new Label(value);
            valLbl.style.fontSize = 11;
            valLbl.style.color = new StyleColor(Color.white);
            valLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(valLbl);

            parent.Add(row);
            return valLbl;
        }

        private Button CreateButton(string text, Color bg, Color textCol, Action onClick)
        {
            var btn = new Button(onClick);
            btn.text = text;
            btn.style.backgroundColor = new StyleColor(bg);
            btn.style.color = new StyleColor(textCol);
            btn.style.fontSize = 10.5f;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.borderTopLeftRadius = 5;
            btn.style.borderTopRightRadius = 5;
            btn.style.borderBottomLeftRadius = 5;
            btn.style.borderBottomRightRadius = 5;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.paddingTop = 4;
            btn.style.paddingBottom = 4;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            return btn;
        }

        private Button CreateSmallButton(string text, Action onClick)
        {
            var btn = new Button(onClick);
            btn.text = text;
            btn.style.width = 22;
            btn.style.height = 22;
            btn.style.fontSize = 11;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.backgroundColor = new StyleColor(new Color(0.22f, 0.23f, 0.28f));
            btn.style.color = new StyleColor(Color.white);
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            return btn;
        }

        private VisualElement CreatePill(string text, Color bg, Color textCol)
        {
            var pill = new VisualElement();
            pill.style.backgroundColor = new StyleColor(bg);
            pill.style.borderTopLeftRadius = 4;
            pill.style.borderTopRightRadius = 4;
            pill.style.borderBottomLeftRadius = 4;
            pill.style.borderBottomRightRadius = 4;
            pill.style.paddingTop = 1;
            pill.style.paddingBottom = 1;
            pill.style.paddingLeft = 6;
            pill.style.paddingRight = 6;

            var lbl = new Label(text);
            lbl.style.fontSize = 9.5f;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color = new StyleColor(textCol);
            pill.Add(lbl);

            return pill;
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

        private string GenerateReport()
        {
            var helper = IAPHelper.Instance;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Unity IAP Helper Diagnostic Report ===");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Initialized: {helper != null}");
            sb.AppendLine($"Is Connected: {helper?.IsConnected}");
            sb.AppendLine($"Products Loaded: {helper?.ProductsLoaded}");
            sb.AppendLine($"Auto-Restore: {helper?.autoRestorePurchases}");
            sb.AppendLine($"Platform: {Application.platform}");
            if (helper?.products != null)
            {
                sb.AppendLine("Products:");
                foreach (var p in helper.products)
                {
                    bool owned = helper.HasPurchased(p.id);
                    sb.AppendLine($"  - ID: {p.id} | Type: {p.type} | Owned: {owned} | FallbackKey: {p.playerPrefsFallbackKey}");
                }
            }
            sb.AppendLine("===========================================");
            return sb.ToString();
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
