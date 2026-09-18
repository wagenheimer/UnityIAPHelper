using System;

using UnityEditor;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Purchasing;

using Wagenheimer.IAPHelper.UI;

namespace Wagenheimer.IAPHelper.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="IAPHelper"/>. Fully hand-drawn (no attribute-driven default
    /// drawers) so every section has exactly one, color-coded header — including a live summary of
    /// who is listening to OnEntitlementGranted / OnEntitlementRevoked, so it's obvious at a glance
    /// whether a product's events are actually wired to anything.
    /// </summary>
    [CustomEditor(typeof(IAPHelper), true)]
    public class IAPHelperEditor : UnityEditor.Editor
    {
        private SerializedProperty _productsProp;
        private SerializedProperty _initializeOnStartProp;
        private SerializedProperty _autoConfigurePlatformRestoreProp;
        private SerializedProperty _autoRestorePurchasesProp;
        private SerializedProperty _processPendingOnFetchProp;
        private SerializedProperty _logPurchasesFetchFailuresProp;
        private SerializedProperty _enableDebugOverlayProp;

        private bool _showProducts = true;
        private bool _showSettings = true;
        private string _productSearch = "";

        private static readonly Color ColorAccentBlue = new Color(0.204f, 0.463f, 0.902f);
        private static readonly Color ColorConsumable = new Color(0.278f, 0.827f, 0.902f);
        private static readonly Color ColorNonConsumable = new Color(0.298f, 0.851f, 0.392f);
        private static readonly Color ColorSubscription = new Color(0.647f, 0.408f, 0.937f);
        private static readonly Color ColorGood = new Color(0.298f, 0.851f, 0.392f);
        private static readonly Color ColorBad = new Color(0.937f, 0.325f, 0.314f);
        private static readonly Color ColorNeutral = new Color(0.647f, 0.686f, 0.741f);

        private void OnEnable()
        {
            _productsProp                    = serializedObject.FindProperty("products");
            _initializeOnStartProp           = serializedObject.FindProperty("initializeOnStart");
            _autoConfigurePlatformRestoreProp= serializedObject.FindProperty("autoConfigurePlatformRestore");
            _autoRestorePurchasesProp        = serializedObject.FindProperty("autoRestorePurchases");
            _processPendingOnFetchProp       = serializedObject.FindProperty("processPendingOnFetch");
            _logPurchasesFetchFailuresProp   = serializedObject.FindProperty("logPurchasesFetchFailures");
            _enableDebugOverlayProp          = serializedObject.FindProperty("enableDebugOverlay");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var helper = (IAPHelper)target;

            DrawHeader();
            DrawToolbar(helper);

            EditorGUILayout.Space(8);

            DrawProductsSection(helper);

            EditorGUILayout.Space(8);

            DrawSettingsSection();

            EditorGUILayout.Space(8);

            DrawRuntimeStatus(helper);

            serializedObject.ApplyModifiedProperties();
        }

        // ── Header & Toolbar ─────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.35f, 0.75f, 1f) : new Color(0.1f, 0.35f, 0.75f) }
            };

            EditorGUILayout.LabelField("🛒 IAP Helper v1.3.0", titleStyle);
            EditorGUILayout.LabelField("Production-ready monetization framework with multi-product zero-code support.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar(IAPHelper helper)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent(" Dashboard", EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image), GUILayout.Height(26)))
                IAPHelperDashboardWindow.OpenDashboard();

            if (GUILayout.Button(new GUIContent(" Run Audit", EditorGUIUtility.IconContent("d_TestPassed").image), GUILayout.Height(26)))
                IAPHelperDashboardWindow.OpenAuditTab();

            bool overlayActive = FindObjectOfType<IAPDebugOverlay>() != null;
            using (new EditorGUI.DisabledScope(true))
            {
                string label = overlayActive ? " Debug Overlay: ON" : " Debug Overlay: OFF";
                string icon  = overlayActive ? "d_DebuggerAttached" : "d_DebuggerDisabled";
                GUILayout.Button(new GUIContent(label, EditorGUIUtility.IconContent(icon).image), GUILayout.Height(26));
            }

            EditorGUILayout.EndHorizontal();
        }

        // ── Card primitive (colored left accent bar, no duplicated headers) ─────────

        private void BeginCard(string title, Color accent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var titleStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = accent }, fontSize = 11 };
            EditorGUILayout.LabelField(title.ToUpperInvariant(), titleStyle);
            EditorGUILayout.Space(2);
        }

        private void EndCard(Color accent)
        {
            EditorGUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                var rect = GUILayoutUtility.GetLastRect();
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), accent);
            }
            EditorGUILayout.Space(4);
        }

        // ── Products ──────────────────────────────────────────────────────────────

        private void DrawProductsSection(IAPHelper helper)
        {
            _showProducts = EditorGUILayout.BeginFoldoutHeaderGroup(_showProducts, $"Products Catalog ({_productsProp.arraySize})");
            if (_showProducts)
            {
                EditorGUILayout.HelpBox(
                    "Each product below can call your game's methods with ZERO code: expand a product, scroll to " +
                    "\"On Entitlement Granted\" / \"On Entitlement Revoked\", click + and drag in the object + method to run.",
                    MessageType.Info);

                if (_productsProp.arraySize > 3)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("🔍", GUILayout.Width(18));
                    _productSearch = EditorGUILayout.TextField(_productSearch);
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                        _productSearch = "";
                    EditorGUILayout.EndHorizontal();
                }

                for (int i = 0; i < _productsProp.arraySize; i++)
                {
                    if (!DrawProductElement(helper, i))
                        break; // element removed mid-loop — array indices shifted, redraw next frame
                }

                EditorGUILayout.Space(4);
                GUI.backgroundColor = ColorAccentBlue;
                if (GUILayout.Button("+ Add Product", GUILayout.Height(26)))
                {
                    _productsProp.InsertArrayElementAtIndex(_productsProp.arraySize);
                    var newElem = _productsProp.GetArrayElementAtIndex(_productsProp.arraySize - 1);
                    newElem.FindPropertyRelative("id").stringValue                = "new_product";
                    newElem.FindPropertyRelative("type").enumValueIndex           = (int)ProductType.Consumable;
                    newElem.FindPropertyRelative("titleFallback").stringValue     = "";
                    newElem.FindPropertyRelative("priceFallback").stringValue     = "$0.99";
                    newElem.FindPropertyRelative("playerPrefsFallbackKey").stringValue = "";
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        /// <returns>false if the element at <paramref name="index"/> was deleted this frame (caller should stop iterating).</returns>
        private bool DrawProductElement(IAPHelper helper, int index)
        {
            SerializedProperty productElem       = _productsProp.GetArrayElementAtIndex(index);
            SerializedProperty idProp            = productElem.FindPropertyRelative("id");
            SerializedProperty typeProp          = productElem.FindPropertyRelative("type");
            SerializedProperty fallbackPriceProp = productElem.FindPropertyRelative("priceFallback");

            string title = string.IsNullOrEmpty(idProp.stringValue) ? $"Product {index}" : idProp.stringValue;

            if (!string.IsNullOrEmpty(_productSearch) &&
                title.IndexOf(_productSearch, StringComparison.OrdinalIgnoreCase) < 0)
                return true;

            var productType = (ProductType)typeProp.enumValueIndex;
            Color typeColor = productType switch
            {
                ProductType.Consumable => ColorConsumable,
                ProductType.NonConsumable => ColorNonConsumable,
                ProductType.Subscription => ColorSubscription,
                _ => ColorNeutral
            };
            string priceText = !string.IsNullOrEmpty(fallbackPriceProp.stringValue) ? $" · {fallbackPriceProp.stringValue}" : "";

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();

            productElem.isExpanded = EditorGUILayout.Foldout(productElem.isExpanded, title, true, EditorStyles.foldoutHeader);

            var typeBadgeStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = typeColor } };
            GUILayout.Label($"● {productType}{priceText}", typeBadgeStyle, GUILayout.Width(150));

            // Event wiring at-a-glance, always visible even when collapsed.
            var grantedEvt = helper.products[index].onEntitlementGranted;
            var revokedEvt = helper.products[index].onEntitlementRevoked;
            DrawWiredDot(grantedEvt);
            DrawWiredDot(revokedEvt);

            if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
            {
                _productsProp.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return false;
            }
            EditorGUILayout.EndHorizontal();

            if (productElem.isExpanded)
            {
                EditorGUILayout.Space(4);

                BeginCard("Identity", ColorAccentBlue);
                EditorGUILayout.PropertyField(idProp, new GUIContent("Product Id"));
                EditorGUILayout.PropertyField(typeProp, new GUIContent("Type"));
                EndCard(ColorAccentBlue);

                BeginCard("Store SKU Overrides (optional — leave blank to reuse Product Id)", ColorNeutral);
                EditorGUILayout.PropertyField(productElem.FindPropertyRelative("googlePlayId"), new GUIContent("Google Play Id"));
                EditorGUILayout.PropertyField(productElem.FindPropertyRelative("appleId"), new GUIContent("Apple Id"));
                EditorGUILayout.PropertyField(productElem.FindPropertyRelative("amazonId"), new GUIContent("Amazon Id"));
                EndCard(ColorNeutral);

                BeginCard("Fallback Display (Editor / Offline, before store metadata loads)", ColorConsumable);
                EditorGUILayout.PropertyField(productElem.FindPropertyRelative("titleFallback"), new GUIContent("Title"));
                EditorGUILayout.PropertyField(productElem.FindPropertyRelative("descriptionFallback"), new GUIContent("Description"));
                EditorGUILayout.PropertyField(productElem.FindPropertyRelative("priceFallback"), new GUIContent("Price"));
                EndCard(ColorConsumable);

                BeginCard("Persistence", ColorSubscription);
                EditorGUILayout.PropertyField(productElem.FindPropertyRelative("playerPrefsFallbackKey"),
                    new GUIContent("PlayerPrefs Fallback Key", "Optional. Auto-saved as '1' on grant, auto-deleted on revoke, and checked by HasPurchased()."));
                EndCard(ColorSubscription);

                BeginCard("On Entitlement Granted — fires once per product, on purchase OR silent restore", ColorGood);
                DrawEventListenerSummary(grantedEvt, ColorGood, "nothing unlocks the content when this product is granted");
                EditorGUILayout.PropertyField(productElem.FindPropertyRelative("onEntitlementGranted"), GUIContent.none);
                EndCard(ColorGood);

                BeginCard("On Entitlement Revoked — refund, family-sharing cancellation, or a Debug Revoke", ColorBad);
                DrawEventListenerSummary(revokedEvt, ColorBad, "nothing re-locks the content when this product is revoked");
                EditorGUILayout.PropertyField(productElem.FindPropertyRelative("onEntitlementRevoked"), GUIContent.none);
                EndCard(ColorBad);
            }

            EditorGUILayout.EndVertical();
            return true;
        }

        /// <summary>Small colored dot next to the collapsed product header — green if the event has at least one listener, red if not.</summary>
        private void DrawWiredDot(UnityEventBase evt)
        {
            bool wired = evt != null && evt.GetPersistentEventCount() > 0;
            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = wired ? ColorGood : ColorBad } };
            GUILayout.Label(wired ? "●" : "○", style, GUILayout.Width(12));
        }

        /// <summary>
        /// Lists exactly which object.method is bound to <paramref name="evt"/>, with a Ping button to
        /// select it — answers "where is this event wired?" without having to expand the UnityEvent field.
        /// </summary>
        private void DrawEventListenerSummary(UnityEventBase evt, Color okColor, string emptyWarning)
        {
            int count = evt?.GetPersistentEventCount() ?? 0;

            if (count == 0)
            {
                var warnStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = ColorBad } };
                EditorGUILayout.LabelField($"⚠ Not wired — {emptyWarning}.", warnStyle);
                return;
            }

            var okStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = okColor } };
            for (int i = 0; i < count; i++)
            {
                var listenerTarget = evt.GetPersistentTarget(i);
                var methodName = evt.GetPersistentMethodName(i);
                string targetName = listenerTarget != null ? listenerTarget.GetType().Name : "(missing target)";

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"✓ {targetName}.{methodName}()", okStyle);
                using (new EditorGUI.DisabledScope(listenerTarget == null))
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(42)))
                    {
                        EditorGUIUtility.PingObject(listenerTarget);
                        Selection.activeObject = listenerTarget;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ── Settings ──────────────────────────────────────────────────────────────

        private void DrawSettingsSection()
        {
            _showSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showSettings, "Settings & Lifecycle");
            if (_showSettings)
            {
                BeginCard("Lifecycle", ColorAccentBlue);
                EditorGUILayout.PropertyField(_initializeOnStartProp,
                    new GUIContent("Initialize On Start", "Connects to the store automatically in Start(). Turn off only if you call IAPHelper.Instance.Initialize() manually."));
                EditorGUILayout.PropertyField(_autoConfigurePlatformRestoreProp,
                    new GUIContent("Auto Configure Platform Restore", "Sets Auto Restore Purchases below automatically per platform at runtime (recommended)."));

                if (!_autoConfigurePlatformRestoreProp.boolValue)
                {
                    EditorGUILayout.PropertyField(_autoRestorePurchasesProp,
                        new GUIContent("Auto Restore Purchases", "TRUE on Android/Amazon (silent, no prompt). FALSE on iOS/macOS — Apple requires an explicit 'Restore' button."));
                }
                else
                {
                    EditorGUILayout.HelpBox("Evaluated at runtime: TRUE on Android/Amazon, FALSE on iOS/macOS.", MessageType.None);
                }

                EditorGUILayout.PropertyField(_processPendingOnFetchProp,
                    new GUIContent("Process Pending On Fetch", "Confirms leftover pending orders found on FetchPurchases automatically. Keep ON."));
                EditorGUILayout.PropertyField(_logPurchasesFetchFailuresProp,
                    new GUIContent("Log Purchases Fetch Failures", "Verbose logging for FetchPurchases errors. Useful while debugging restore issues."));
                EndCard(ColorAccentBlue);

                BeginCard("Debug & QA", ColorSubscription);
                EditorGUILayout.PropertyField(_enableDebugOverlayProp,
                    new GUIContent("Enable Debug Overlay", "Auto-attaches the in-game IAPDebugOverlay (F10) in Editor and Development Builds. No scene setup needed."));
                if (_enableDebugOverlayProp != null && _enableDebugOverlayProp.boolValue)
                {
                    EditorGUILayout.HelpBox("Press F10 in Play Mode to open the overlay: simulate grants, revokes, and inspect ownership sources per product.", MessageType.None);
                }
                EndCard(ColorSubscription);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ── Runtime Diagnostics ───────────────────────────────────────────────────

        private void DrawRuntimeStatus(IAPHelper helper)
        {
            if (!Application.isPlaying)
                return;

            BeginCard("Live Runtime Diagnostics", ColorAccentBlue);
            DrawStatusRow("Store Connected", helper.IsConnected);
            DrawStatusRow("Products Loaded", helper.ProductsLoaded);
            EditorGUILayout.LabelField("Auto-Restore Setting", helper.autoRestorePurchases.ToString());
            EndCard(ColorAccentBlue);
        }

        private static void DrawStatusRow(string label, bool ok)
        {
            var style = new GUIStyle(EditorStyles.label) { normal = { textColor = ok ? ColorGood : ColorBad }, fontStyle = FontStyle.Bold };
            EditorGUILayout.LabelField(label, ok ? "● YES" : "● NO", style);
        }
    }
}
