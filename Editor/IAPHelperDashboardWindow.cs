using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.IAPHelper.Editor
{
    /// <summary>
    /// Unified modern dashboard and verification center for IAP Helper.
    /// Provides interactive project auditing, catalog inspection, persistent store release checklists,
    /// and documentation / update tools built entirely with UI Toolkit.
    /// </summary>
    public class IAPHelperDashboardWindow : EditorWindow
    {
        private enum Tab
        {
            SetupAudit,
            ProductCatalog,
            StoreChecklist,
            UpdatesAndDocs
        }

        private Tab _currentTab = Tab.SetupAudit;
        private VisualElement _root;
        private ScrollView _contentContainer;

        [MenuItem("Tools/Wagenheimer/IAP Helper/Dashboard", priority = 120)]
        [MenuItem("Window/Wagenheimer/IAP Helper/Dashboard", priority = 210)]
        public static void OpenDashboard()
        {
            var window = GetWindow<IAPHelperDashboardWindow>("IAP Helper");
            window.minSize = new Vector2(620, 520);
            window.titleContent = new GUIContent("IAP Helper", EditorGUIUtility.IconContent("d_Favorite").image);
            window.Show();
        }

        public static void OpenAuditTab()
        {
            var window = GetWindow<IAPHelperDashboardWindow>("IAP Helper");
            window.minSize = new Vector2(620, 520);
            window.titleContent = new GUIContent("IAP Helper", EditorGUIUtility.IconContent("d_Favorite").image);
            window._currentTab = Tab.SetupAudit;
            window.Show();
            window.RebuildUI();
        }

        public void CreateGUI()
        {
            _root = rootVisualElement;
            _root.style.flexGrow = 1;
            IAPHelperUIStyle.Apply(_root);

            RebuildUI();
        }

        private void RebuildUI()
        {
            _root.Clear();

            // 1. Header Banner
            _root.Add(CreateHeaderBanner());

            // 2. Tab Bar
            _root.Add(CreateTabBar());

            // 3. ScrollView Content Container
            _contentContainer = new ScrollView(ScrollViewMode.Vertical);
            _contentContainer.style.flexGrow = 1;
            _root.Add(_contentContainer);

            RebuildContent();
        }

        private VisualElement CreateHeaderBanner()
        {
            var banner = new VisualElement();
            banner.AddToClassList("iap-header");

            var row = new VisualElement();
            row.AddToClassList("iap-header-row");

            var left = new VisualElement();
            left.AddToClassList("iap-header-left");

            var icon = new Label("💎") { style = { fontSize = 20, marginRight = 8 } };
            var title = new Label("Unity IAP Helper");
            title.AddToClassList("iap-header-title");
            left.Add(icon);
            left.Add(title);

            var verBadge = new Label("v" + GetPackageVersion());
            verBadge.AddToClassList("iap-header-version");
            left.Add(verBadge);
            row.Add(left);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("iap-toolbar-actions");

            var updateBtn = new Button(() => UpdateChecker.CheckForUpdate(force: true)) { text = "🔄 Updates" };
            updateBtn.AddToClassList("iap-toolbar-btn");
            toolbar.Add(updateBtn);

            row.Add(toolbar);
            banner.Add(row);

            var subtitle = new Label("Multi-store in-app purchases, two-step pending-confirm flow, and automated release verification.");
            subtitle.AddToClassList("iap-header-subtitle");
            banner.Add(subtitle);

            return banner;
        }

        private VisualElement CreateTabBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("iap-tab-row");

            (Tab tab, string icon, string title)[] tabs =
            {
                (Tab.SetupAudit, "🔍", "Setup Audit"),
                (Tab.ProductCatalog, "📦", "Product Catalog"),
                (Tab.StoreChecklist, "📋", "Store Checklist"),
                (Tab.UpdatesAndDocs, "📚", "Docs & Updates")
            };

            foreach (var t in tabs)
            {
                var tabEnum = t.tab;
                var btn = new Button(() =>
                {
                    _currentTab = tabEnum;
                    RebuildUI();
                })
                { text = $"{t.icon} {t.title}" };

                btn.AddToClassList("iap-tab-btn");
                if (_currentTab == tabEnum)
                {
                    btn.AddToClassList("active");
                }

                bar.Add(btn);
            }

            return bar;
        }

        private void RebuildContent()
        {
            _contentContainer.Clear();

            switch (_currentTab)
            {
                case Tab.SetupAudit:
                    _contentContainer.Add(new IAPHelperAuditView().Root);
                    break;
                case Tab.ProductCatalog:
                    _contentContainer.Add(new IAPHelperCatalogView().Root);
                    break;
                case Tab.StoreChecklist:
                    _contentContainer.Add(new IAPHelperChecklistView().Root);
                    break;
                case Tab.UpdatesAndDocs:
                    _contentContainer.Add(new IAPHelperDocsView().Root);
                    break;
            }
        }

        private static string GetPackageVersion()
        {
            try
            {
                var packageJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.wagenheimer.iaphelper/package.json");
                if (packageJson != null)
                {
                    var data = JsonUtility.FromJson<PackageJsonMinimal>(packageJson.text);
                    if (data != null && !string.IsNullOrEmpty(data.version))
                        return data.version;
                }
            }
            catch { }
            return "1.8.0";
        }

        [Serializable]
        private class PackageJsonMinimal
        {
            public string version;
        }
    }
}
