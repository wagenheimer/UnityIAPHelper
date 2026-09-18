using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;

namespace Wagenheimer.IAPHelper.Editor
{
    /// <summary>
    /// Unified modern dashboard and verification center for IAP Helper.
    /// Provides interactive project auditing, catalog inspection, persistent store release checklists,
    /// and documentation / update tools.
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
        private Vector2 _scrollPos;

        // Audit state
        private List<AuditResult> _auditResults;
        private AuditSeverity? _severityFilter;
        private string _searchFilter = "";

        // Checklist items definition
        private static readonly (string Category, string Id, string Label, string Description)[] StoreChecklistItems = new[]
        {
            // Google Play
            ("Google Play Console", "gp_active", "Product is 'Active' in In-App Products", "Products marked Inactive return no price and cannot be purchased."),
            ("Google Play Console", "gp_track", "App uploaded to at least one test track", "Internal or Closed test track with matching package name and signing key."),
            ("Google Play Console", "gp_tester", "Test account added to License Testing", "Settings > License Testing. Without this, real charges occur or purchases fail."),
            ("Google Play Console", "gp_restore", "Tested buy → restart cycle", "Android auto-restores silently on launch. Verify content is unlocked on clean boot."),

            // Apple App Store
            ("Apple App Store Connect", "apple_sku", "Product created with identical Product ID", "Matches either 'appleId' override or the main 'id' character-for-character."),
            ("Apple App Store Connect", "apple_status", "Status is 'Ready to Submit'", "Metadata, description, and review screenshot must be filled in."),
            ("Apple App Store Connect", "apple_contract", "Paid Applications Agreement signed", "Agreements, Tax, and Banking must show active status; otherwise sandbox fails."),
            ("Apple App Store Connect", "apple_sandbox", "Tested with dedicated Sandbox Tester account", "Do not test using a personal Apple ID."),
            ("Apple App Store Connect", "apple_restore_btn", "Restore Purchases button is visible in UI", "Mandatory per App Store Review Guidelines 3.1.1."),

            // General
            ("General / Multiplatform", "gen_case", "Product IDs are exact case-sensitive matches", "Mismatched casing is the most common reason for failed product lookup."),
            ("General / Multiplatform", "gen_save", "Entitlement is permanently wired", "Reward method is hooked via OnEntitlementGranted or product UnityEvent.")
        };

        [MenuItem("Window/Wagenheimer/IAP Helper Dashboard", priority = 10)]
        [MenuItem("Tools/Wagenheimer/IAP Helper/Dashboard & Verification...", priority = 10)]
        public static void OpenDashboard()
        {
            var window = GetWindow<IAPHelperDashboardWindow>("IAP Helper");
            window.minSize = new Vector2(580, 520);
            window.Show();
        }

        public static void OpenAuditTab()
        {
            var window = GetWindow<IAPHelperDashboardWindow>("IAP Helper");
            window._currentTab = Tab.SetupAudit;
            window.RunAudit();
            window.Show();
        }

        private void OnEnable()
        {
            if (_auditResults == null)
            {
                RunAudit();
            }
        }

        private void RunAudit()
        {
            _auditResults = IAPHelperAudit.RunAudit();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawTabBar();

            EditorGUILayout.Space(6);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_currentTab)
            {
                case Tab.SetupAudit:
                    DrawSetupAuditTab();
                    break;

                case Tab.ProductCatalog:
                    DrawProductCatalogTab();
                    break;

                case Tab.StoreChecklist:
                    DrawStoreChecklistTab();
                    break;

                case Tab.UpdatesAndDocs:
                    DrawUpdatesAndDocsTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        #region Header & Tab Bar

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.35f, 0.75f, 1f) : new Color(0.1f, 0.35f, 0.75f) }
            };

            EditorGUILayout.LabelField("IAP Helper Dashboard", titleStyle, GUILayout.Height(24));
            GUILayout.FlexibleSpace();

            GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.gray }
            };
            EditorGUILayout.LabelField("v1.2.0", badgeStyle, GUILayout.Width(45));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Verification Center, Multi-Product Management & Store Release Checklist", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal();

            string[] tabNames = { "Setup Audit", "Product Catalog", "Store Checklist", "Docs & Updates" };
            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab, tabNames, GUILayout.Height(28));

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Tab 1: Setup Audit

        private void DrawSetupAuditTab()
        {
            if (_auditResults == null)
                RunAudit();

            int passCount = _auditResults.Count(r => r.Severity == AuditSeverity.Pass);
            int infoCount = _auditResults.Count(r => r.Severity == AuditSeverity.Info);
            int warnCount = _auditResults.Count(r => r.Severity == AuditSeverity.Warning);
            int failCount = _auditResults.Count(r => r.Severity == AuditSeverity.Fail);

            // Summary Bar
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            DrawCountBadge("Pass", passCount, Color.green);
            DrawCountBadge("Info", infoCount, Color.cyan);
            DrawCountBadge("Warning", warnCount, Color.yellow);
            DrawCountBadge("Fail", failCount, failCount > 0 ? Color.red : Color.gray);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Re-run Audit", GUILayout.Width(100), GUILayout.Height(24)))
            {
                RunAudit();
            }

            if (GUILayout.Button("Export Markdown", GUILayout.Width(110), GUILayout.Height(24)))
            {
                string md = IAPHelperAudit.ToMarkdown(_auditResults);
                EditorGUIUtility.systemCopyBuffer = md;
                EditorUtility.DisplayDialog("Audit Report", "Markdown report copied to clipboard!", "OK");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Filters
            EditorGUILayout.BeginHorizontal();
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter);

            if (GUILayout.Button("All", _severityFilter == null ? EditorStyles.miniButtonMid : EditorStyles.miniButton, GUILayout.Width(40)))
                _severityFilter = null;
            if (GUILayout.Button("Fails", _severityFilter == AuditSeverity.Fail ? EditorStyles.miniButtonMid : EditorStyles.miniButton, GUILayout.Width(50)))
                _severityFilter = AuditSeverity.Fail;
            if (GUILayout.Button("Warnings", _severityFilter == AuditSeverity.Warning ? EditorStyles.miniButtonMid : EditorStyles.miniButton, GUILayout.Width(65)))
                _severityFilter = AuditSeverity.Warning;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Filtered results
            var filtered = _auditResults.Where(r =>
            {
                if (_severityFilter.HasValue && r.Severity != _severityFilter.Value)
                    return false;
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    string search = _searchFilter.ToLower();
                    return (r.Title != null && r.Title.ToLower().Contains(search)) ||
                           (r.Category != null && r.Category.ToLower().Contains(search)) ||
                           (r.Detail != null && r.Detail.ToLower().Contains(search));
                }
                return true;
            }).ToList();

            if (filtered.Count == 0)
            {
                EditorGUILayout.HelpBox("No audit items match your filter.", MessageType.Info);
                return;
            }

            foreach (var item in filtered)
            {
                DrawAuditItemBox(item);
            }
        }

        private void DrawCountBadge(string label, int count, Color color)
        {
            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = color } };
            EditorGUILayout.LabelField($"{label}: {count}", style, GUILayout.Width(85));
        }

        private void DrawAuditItemBox(AuditResult item)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            string icon = item.Severity switch
            {
                AuditSeverity.Pass => "✓",
                AuditSeverity.Info => "ℹ",
                AuditSeverity.Warning => "⚠",
                AuditSeverity.Fail => "✕",
                _ => "•"
            };

            string colorName = item.Severity switch
            {
                AuditSeverity.Pass => "green",
                AuditSeverity.Info => "cyan",
                AuditSeverity.Warning => "yellow",
                AuditSeverity.Fail => "red",
                _ => "white"
            };

            EditorGUILayout.LabelField($"<color={colorName}><b>[{icon}] {item.Category}</b></color>: {item.Title}",
                new GUIStyle(EditorStyles.boldLabel) { richText = true });
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(item.Detail))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(item.Detail, EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
            }

            if (!string.IsNullOrEmpty(item.FixHint))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox($"Fix: {item.FixHint}", MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Tab 2: Product Catalog

        private void DrawProductCatalogTab()
        {
            var helper = FindObjectOfType<IAPHelper>();

            if (helper == null)
            {
                EditorGUILayout.HelpBox("No IAPHelper component found in active scene. Place an IAPHelper in your preloading or manager scene.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Found IAPHelper on: <b>{helper.gameObject.name}</b> ({helper.products?.Count ?? 0} products)", new GUIStyle(EditorStyles.label) { richText = true });
            if (GUILayout.Button("Select Component", GUILayout.Width(130)))
            {
                Selection.activeGameObject = helper.gameObject;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            if (helper.products == null || helper.products.Count == 0)
            {
                EditorGUILayout.HelpBox("Products catalog is empty. Add products in the IAPHelper Inspector.", MessageType.Info);
                return;
            }

            foreach (var product in helper.products)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"<b>{product.id}</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Type: <color=cyan>{product.type}</color>", new GUIStyle(EditorStyles.label) { richText = true }, GUILayout.Width(130));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"Price Fallback: {(!string.IsNullOrEmpty(product.priceFallback) ? product.priceFallback : "(none)")} | Title: {(!string.IsNullOrEmpty(product.titleFallback) ? product.titleFallback : "(none)")}");

                string googleSku = !string.IsNullOrEmpty(product.googlePlayId) ? product.googlePlayId : product.id;
                string appleSku = !string.IsNullOrEmpty(product.appleId) ? product.appleId : product.id;
                string amazonSku = !string.IsNullOrEmpty(product.amazonId) ? product.amazonId : product.id;

                EditorGUILayout.LabelField($"SKUs → Google Play: <color=grey>{googleSku}</color> | Apple: <color=grey>{appleSku}</color> | Amazon: <color=grey>{amazonSku}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true });

                if (!string.IsNullOrEmpty(product.playerPrefsFallbackKey))
                {
                    EditorGUILayout.LabelField($"Persistence: Auto PlayerPrefs key '{product.playerPrefsFallbackKey}'", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
            }
        }

        #endregion

        #region Tab 3: Store Checklist

        private void DrawStoreChecklistTab()
        {
            EditorGUILayout.HelpBox("Pre-flight checklist before submitting to stores. Checkbox progress is saved locally per project.", MessageType.Info);

            int total = StoreChecklistItems.Length;
            int completed = StoreChecklistItems.Count(item => IsChecklistChecked(item.Id));
            float progress = (float)completed / total;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(18));
            EditorGUI.ProgressBar(r, progress, $"{completed} of {total} completed ({(int)(progress * 100)}%)");

            if (GUILayout.Button("Reset", GUILayout.Width(60), GUILayout.Height(18)))
            {
                if (EditorUtility.DisplayDialog("Reset Checklist", "Are you sure you want to reset all checklist items?", "Yes", "Cancel"))
                {
                    foreach (var item in StoreChecklistItems)
                        SetChecklistChecked(item.Id, false);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            string currentCategory = null;

            foreach (var item in StoreChecklistItems)
            {
                if (item.Category != currentCategory)
                {
                    currentCategory = item.Category;
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField(currentCategory, EditorStyles.boldLabel);
                }

                bool isChecked = IsChecklistChecked(item.Id);
                bool newChecked = EditorGUILayout.ToggleLeft($" {item.Label}", isChecked, EditorStyles.boldLabel);
                if (newChecked != isChecked)
                {
                    SetChecklistChecked(item.Id, newChecked);
                }

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(item.Description, EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
            }
        }

        private bool IsChecklistChecked(string id)
        {
            string key = $"Wagenheimer.IAPHelper.Checklist.{Application.identifier}.{id}";
            return EditorPrefs.GetBool(key, false);
        }

        private void SetChecklistChecked(string id, bool val)
        {
            string key = $"Wagenheimer.IAPHelper.Checklist.{Application.identifier}.{id}";
            EditorPrefs.SetBool(key, val);
        }

        #endregion

        #region Tab 4: Updates & Docs

        private void DrawUpdatesAndDocsTab()
        {
            EditorGUILayout.LabelField("Package & Documentation", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Installed Version: <b>1.2.0</b>", new GUIStyle(EditorStyles.label) { richText = true });
            EditorGUILayout.LabelField("Repository: https://github.com/wagenheimer/UnityIAPHelper", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Check GitHub for Updates", GUILayout.Height(24)))
            {
                UpdateChecker.CheckForUpdate(force: true);
            }

            if (GUILayout.Button("Open GitHub Repo", GUILayout.Height(24)))
            {
                Application.OpenURL("https://github.com/wagenheimer/UnityIAPHelper");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Guides & Documentation", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (GUILayout.Button("View Package README.md", GUILayout.Height(24)))
            {
                OpenRelativeFile("README.md");
            }

            if (GUILayout.Button("View Full IAP-CHECKLIST.md", GUILayout.Height(24)))
            {
                OpenRelativeFile("IAP-CHECKLIST.md");
            }

            if (GUILayout.Button("View CHANGELOG.md", GUILayout.Height(24)))
            {
                OpenRelativeFile("CHANGELOG.md");
            }

            if (GUILayout.Button("Official Unity IAP Documentation", GUILayout.Height(24)))
            {
                Application.OpenURL("https://docs.unity.com/packages/com.unity.purchasing/manual/index.html");
            }

            EditorGUILayout.EndVertical();
        }

        private void OpenRelativeFile(string filename)
        {
            var guids = AssetDatabase.FindAssets("t:DefaultAsset " + System.IO.Path.GetFileNameWithoutExtension(filename));
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(filename, StringComparison.OrdinalIgnoreCase))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (obj != null)
                    {
                        AssetDatabase.OpenAsset(obj);
                        return;
                    }
                }
            }

            Application.OpenURL($"https://github.com/wagenheimer/UnityIAPHelper/blob/main/{filename}");
        }

        #endregion
    }
}
