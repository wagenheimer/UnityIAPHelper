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

            var badge = IAPHelperUIStyle.CreateBadge("v1.8.0", "info");
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
            var eventCard = IAPHelperUIStyle.CreateCard("⚡ Global Lifecycle Events");
            eventCard.Add(new PropertyField(serializedObject.FindProperty("onInitialized")));
            eventCard.Add(new PropertyField(serializedObject.FindProperty("onInitializeFailed")));
            eventCard.Add(new PropertyField(serializedObject.FindProperty("onPurchaseSuccess")));
            eventCard.Add(new PropertyField(serializedObject.FindProperty("onPurchaseFailed")));
            eventCard.Add(new PropertyField(serializedObject.FindProperty("onPurchaseCancelled")));
            eventCard.Add(new PropertyField(serializedObject.FindProperty("onRestoreCompleted")));
            root.Add(eventCard);

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
    }
}
