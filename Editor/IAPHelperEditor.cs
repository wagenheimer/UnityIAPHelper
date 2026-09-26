using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Wagenheimer.IAPHelper.UI;

namespace Wagenheimer.IAPHelper.Editor
{
    /// <summary>
    /// Custom UI Toolkit Inspector for <see cref="IAPHelper"/>.
    /// Provides data binding, live wiring feedback, quick dashboard triggers, and settings cards.
    /// </summary>
    [CustomEditor(typeof(IAPHelper), true)]
    public class IAPHelperEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            IAPHelperUIStyle.Apply(root);

            var helper = (IAPHelper)target;

            // 1. Hero Header Banner
            var header = new VisualElement();
            header.AddToClassList("iap-header");

            var row = new VisualElement();
            row.AddToClassList("iap-header-row");

            var left = new VisualElement();
            left.AddToClassList("iap-header-left");

            var icon = new Label("💎") { style = { fontSize = 18, marginRight = 6 } };
            var title = new Label("IAP Helper");
            title.AddToClassList("iap-header-title");
            left.Add(icon);
            left.Add(title);

            var badge = IAPHelperUIStyle.CreateBadge("v" + IAPHelperDashboardWindow.GetPackageVersion(), "info");
            left.Add(badge);
            row.Add(left);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("iap-toolbar-actions");

            var dashBtn = new Button(IAPHelperDashboardWindow.OpenDashboard) { text = "📊 Dashboard" };
            dashBtn.AddToClassList("iap-toolbar-btn");
            toolbar.Add(dashBtn);

            var auditBtn = new Button(IAPHelperDashboardWindow.OpenAuditTab) { text = "🔍 Audit" };
            auditBtn.AddToClassList("iap-toolbar-btn");
            toolbar.Add(auditBtn);

            row.Add(toolbar);
            header.Add(row);

            var sub = new Label("Production-ready in-app purchases with two-step pending-confirm flow.");
            sub.AddToClassList("iap-header-subtitle");
            header.Add(sub);

            root.Add(header);

            // 2. Products Catalog Card
            var prodCard = IAPHelperUIStyle.CreateCard("📦 Product Catalog", "Configure in-app items, store-specific IDs, and entitlement callbacks.");
            var prodProp = serializedObject.FindProperty("products");
            prodCard.Add(new PropertyField(prodProp));
            root.Add(prodCard);

            // 3. Lifecycle & Initialization Settings
            var initCard = IAPHelperUIStyle.CreateCard("⚙️ Initialization & Platform Settings");
            initCard.Add(new PropertyField(serializedObject.FindProperty("initializeOnStart"), "Initialize on Start"));
            initCard.Add(new PropertyField(serializedObject.FindProperty("autoConfigurePlatformRestore"), "Auto-Configure Platform Restore"));
            initCard.Add(new PropertyField(serializedObject.FindProperty("autoRestorePurchases"), "Auto-Restore Purchases"));
            initCard.Add(new PropertyField(serializedObject.FindProperty("processPendingOnFetch"), "Process Pending on Fetch"));
            initCard.Add(new PropertyField(serializedObject.FindProperty("logPurchasesFetchFailures"), "Log Fetch Failures"));
            initCard.Add(new PropertyField(serializedObject.FindProperty("enableDebugOverlay"), "Enable In-Game Debug Overlay"));

            initCard.Add(IAPHelperUIStyle.CreateCallout(
                "• Android (Google Play / Amazon): Auto-Restore is recommended TRUE (silent restore on reinstall).\n" +
                "• iOS (Apple App Store): Auto-Restore should be FALSE per Apple guidelines (manual restore button required).",
                "info"));

            root.Add(initCard);

            // 4. Global Events Card
            var eventCard = IAPHelperUIStyle.CreateCard("⚡ Events", "Every IAPHelper event in one place. Reactions for one specific product live on that product above.");
            var eventsProp = serializedObject.FindProperty("globalEvents");

            var wiringSummary = new Label();
            wiringSummary.style.whiteSpace = WhiteSpace.Normal;
            wiringSummary.style.fontSize = 11;
            wiringSummary.style.marginBottom = 6;
            eventCard.Add(wiringSummary);
            eventCard.Add(new PropertyField(eventsProp));
            root.Add(eventCard);

            void RefreshWiringSummary() => wiringSummary.text = BuildWiringSummary(eventsProp, serializedObject.FindProperty("products"));
            RefreshWiringSummary();
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshWiringSummary());

            // 5. Play Mode Diagnostics (if running)
            if (Application.isPlaying)
            {
                var playCard = IAPHelperUIStyle.CreateCard("🎮 Play Mode Diagnostics");
                bool isInit = helper.IsInitialized;
                playCard.Add(IAPHelperUIStyle.CreateBadge(isInit ? "Store Connected: YES" : "Store Connected: NO", isInit ? "pass" : "warn"));

                var fetchBtn = new Button(() => helper.FetchPurchases()) { text = "🔄 Fetch Purchases Now" };
                fetchBtn.AddToClassList("iap-toolbar-btn");
                fetchBtn.style.marginTop = 6;
                playCard.Add(fetchBtn);

                root.Add(playCard);
            }

            return root;
        }

        private const string PersistentCallsPath = "m_PersistentCalls.m_Calls";

        /// <summary>
        /// One-glance overview of which Inspector events already have listeners wired: the global
        /// events, plus the granted/revoked events of each product.
        /// </summary>
        private static string BuildWiringSummary(SerializedProperty globalEvents, SerializedProperty products)
        {
            var wired = new System.Collections.Generic.List<string>();
            int total = 0;

            var child = globalEvents.Copy();
            var end = globalEvents.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                total++;

                int count = ListenerCount(child);
                if (count > 0)
                    wired.Add($"{child.name} ({count})");
            }

            var text = $"Global events wired: {wired.Count} of {total}" +
                       (wired.Count > 0 ? "\n" + string.Join(", ", wired) : "");

            if (products != null)
            {
                for (int i = 0; i < products.arraySize; i++)
                {
                    var product = products.GetArrayElementAtIndex(i);
                    var id = product.FindPropertyRelative("id")?.stringValue;
                    int granted = ListenerCount(product.FindPropertyRelative("onEntitlementGranted"));
                    int revoked = ListenerCount(product.FindPropertyRelative("onEntitlementRevoked"));
                    text += $"\nProduct '{id}': granted {granted}, revoked {revoked}";
                }
            }

            return text;
        }

        private static int ListenerCount(SerializedProperty unityEvent)
        {
            var calls = unityEvent?.FindPropertyRelative(PersistentCallsPath);
            return calls != null ? calls.arraySize : 0;
        }
    }
}
