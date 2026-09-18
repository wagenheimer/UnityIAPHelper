using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wagenheimer.IAPHelper.Editor
{
    public enum AuditSeverity
    {
        Pass,
        Info,
        Warning,
        Fail
    }

    public struct AuditResult
    {
        public string Category;
        public string Title;
        public AuditSeverity Severity;
        public string Detail;
        public string FixHint;
    }

    /// <summary>
    /// Static, headless-friendly project audit for IAP Helper setup. Scans the CONSUMING project
    /// (prefabs, scenes under Assets/, and C# source under Assets/) for the configuration mistakes that
    /// most commonly cause the "bought the game but ads/gates never lift" class of bug: mismatched product
    /// IDs between <see cref="IAPHelper"/> and <see cref="BaseIAPForm"/>, missing catalog entries, no local
    /// fallback/restore wiring, or an outdated package version that lacks the persistent grant/confirm fix.
    /// </summary>
    /// <remarks>
    /// Runnable from an automated/CI context (including AI coding agents) via Unity batch mode:
    /// <code>
    /// Unity -batchmode -quit -projectPath "&lt;project&gt;" -logFile - \
    ///   -executeMethod Wagenheimer.IAPHelper.Editor.IAPHelperAudit.RunHeadlessAndLog
    /// </code>
    /// The full checklist (including the parts that cannot be verified from source, like the store
    /// consoles) lives in <c>IAP-CHECKLIST.md</c> at the package root.
    /// </remarks>
    public static class IAPHelperAudit
    {
        [MenuItem("Tools/Wagenheimer/IAP Helper/Verify Setup...", priority = 100)]
        public static void OpenWindow()
        {
            IAPHelperAuditWindow.ShowWindow(RunAudit());
        }

        /// <summary>
        /// Runs the audit and writes a Markdown report to the Console/log. Intended for
        /// <c>-executeMethod</c> batch-mode invocation from CI or from an AI agent that has shell
        /// access to the Unity Editor but not to the running Editor UI.
        /// </summary>
        public static void RunHeadlessAndLog()
        {
            var results = RunAudit();
            Debug.Log(ToMarkdown(results));

            if (results.Any(r => r.Severity == AuditSeverity.Fail))
                EditorApplication.Exit(1);
        }

        public static List<AuditResult> RunAudit()
        {
            var results = new List<AuditResult>();

            AuditPurchasingPackage(results);

            var helperInstances = FindAllComponents<IAPHelper>();
            var formInstances = FindAllComponents<BaseIAPForm>();
            var csFiles = SafeGetAllScripts();

            var runtimeAddComponentSites = FindRuntimeAddComponentSites(csFiles);
            var explicitProductsOverrideSites = FindProductsOverrideSites(csFiles);

            var effectiveCatalogIds = AuditHelperInstances(
                results, helperInstances, runtimeAddComponentSites, explicitProductsOverrideSites);

            AuditFormInstances(results, formInstances);
            AuditProductIdCrossReference(results, formInstances, effectiveCatalogIds);
            AuditSourceWiring(results, csFiles);
            AuditPackageVersion(results);

            return results;
        }

        #region Checks

        private static void AuditPurchasingPackage(List<AuditResult> results)
        {
            var listRequest = Client.List(true, false);
            while (!listRequest.IsCompleted)
            {
                // Synchronous wait is acceptable here: this only runs from an explicit user/CI action,
                // never on the hot path, and Client.List(offlineMode:true) resolves near-instantly.
                System.Threading.Thread.Sleep(10);
            }

            if (listRequest.Status != StatusCode.Success)
            {
                results.Add(new AuditResult
                {
                    Category = "Unity Purchasing",
                    Title = "Could not list installed packages",
                    Severity = AuditSeverity.Warning,
                    Detail = listRequest.Error?.message ?? "Unknown error querying the Package Manager.",
                    FixHint = "Re-run while the Package Manager is idle (not in the middle of an add/remove operation)."
                });
                return;
            }

            var purchasing = listRequest.Result.FirstOrDefault(p => p.name == "com.unity.purchasing");
            if (purchasing == null)
            {
                results.Add(new AuditResult
                {
                    Category = "Unity Purchasing",
                    Title = "com.unity.purchasing is not installed",
                    Severity = AuditSeverity.Fail,
                    Detail = "IAPHelper depends on the official Unity IAP v5+ package.",
                    FixHint = "Window > Package Manager > Unity Registry > In App Purchasing, install version 5.4.3 or later."
                });
                return;
            }

            var minVersion = new Version(5, 4, 3);
            var installedVersionOk = Version.TryParse(purchasing.version, out var installedVersion) && installedVersion >= minVersion;

            results.Add(new AuditResult
            {
                Category = "Unity Purchasing",
                Title = $"com.unity.purchasing {purchasing.version} installed",
                Severity = installedVersionOk ? AuditSeverity.Pass : AuditSeverity.Fail,
                Detail = installedVersionOk
                    ? "Version compatible with the Pending -> Confirm flow (StoreController v5)."
                    : $"Version {purchasing.version} predates the minimum supported version (5.4.3). The v5 API (StoreController, Orders, PendingOrder) may not exist in this version.",
                FixHint = installedVersionOk ? null : "Update via Window > Package Manager > In App Purchasing > Update."
            });
        }

        /// <returns>
        /// The effective set of product IDs known to be in some catalog, used by
        /// <see cref="AuditProductIdCrossReference"/>. Combines catalogs read from serialized components with
        /// IDs found via static source scanning (see remarks on the "no serialized instance" fallback below).
        /// </returns>
        private static HashSet<string> AuditHelperInstances(
            List<AuditResult> results,
            List<FoundComponent<IAPHelper>> helperInstances,
            List<string> runtimeAddComponentSites,
            List<string> explicitProductsOverrideSites)
        {
            var catalogIds = new HashSet<string>();

            if (helperInstances.Count == 0)
            {
                if (runtimeAddComponentSites.Count > 0)
                {
                    // IAPHelper is instantiated purely via code (gameObject.AddComponent<IAPHelper>()), which
                    // is a fully supported usage pattern — it just means there is no prefab/scene asset to
                    // read its 'products' catalog from. Fall back to a temporary scratch instance to read
                    // whatever catalog the component would have at runtime if nothing overrides it in code.
                    var (defaultCatalog, instantiationError) = TryReadDefaultProductCatalog();

                    results.Add(new AuditResult
                    {
                        Category = "IAPHelper",
                        Title = "IAPHelper is added at runtime via AddComponent<IAPHelper>()",
                        Severity = AuditSeverity.Pass,
                        Detail = "Found in:\n" + string.Join("\n", runtimeAddComponentSites.Select(s => $"- {s}")) +
                                  (instantiationError == null
                                      ? $"\nCatalog read from a scratch instance of the compiled component (its default, unless overridden in code): {string.Join(", ", defaultCatalog)}"
                                      : $"\nCould not read the default catalog from a scratch instance: {instantiationError}"),
                        FixHint = explicitProductsOverrideSites.Count > 0
                            ? "This project also assigns/overrides '.products' in code (see below) — the audit cannot see the runtime value of that override, only the compiled default. Verify the overridden catalog manually."
                            : null
                    });

                    foreach (var id in defaultCatalog)
                        catalogIds.Add(id);
                }
                else
                {
                    results.Add(new AuditResult
                    {
                        Category = "IAPHelper",
                        Title = "No IAPHelper component found in the project",
                        Severity = AuditSeverity.Fail,
                        Detail = "No GameObject with the IAPHelper component was found in prefabs, scenes, or via gameObject.AddComponent<IAPHelper>()/AddComponent(typeof(IAPHelper)) in source.",
                        FixHint = "Add IAPHelper.Instance during game boot (e.g. AddComponent at startup) or place the component on a persistent GameObject (DontDestroyOnLoad is already applied internally)."
                    });
                }
            }
            else if (helperInstances.Count > 1)
            {
                results.Add(new AuditResult
                {
                    Category = "IAPHelper",
                    Title = $"{helperInstances.Count} IAPHelper components found",
                    Severity = AuditSeverity.Warning,
                    Detail = "IAPHelper is a singleton (DontDestroyOnLoad); multiple instances across different prefabs/scenes is only safe if just one of them ever gets instantiated at runtime (the others self-destroy in Awake). Verify this is intentional.",
                    FixHint = string.Join("\n", helperInstances.Select(h => $"- {h.Location}"))
                });
            }
            else
            {
                results.Add(new AuditResult
                {
                    Category = "IAPHelper",
                    Title = "1 IAPHelper component found",
                    Severity = AuditSeverity.Pass,
                    Detail = helperInstances[0].Location
                });
            }

            foreach (var found in helperInstances)
            {
                var helper = found.Component;

                if (helper.products == null || helper.products.Count == 0)
                {
                    results.Add(new AuditResult
                    {
                        Category = "IAPHelper",
                        Title = "Empty product catalog",
                        Severity = AuditSeverity.Fail,
                        Detail = $"'{found.Location}' has no products configured in its 'products' list.",
                        FixHint = "Fill 'products' with at least one ProductConfig whose 'id' matches the store SKU."
                    });
                    continue;
                }

                foreach (var p in helper.products)
                {
                    if (!string.IsNullOrWhiteSpace(p.id))
                        catalogIds.Add(p.id);
                }

                var emptyIds = helper.products.Where(p => string.IsNullOrWhiteSpace(p.id)).ToList();
                if (emptyIds.Count > 0)
                {
                    results.Add(new AuditResult
                    {
                        Category = "IAPHelper",
                        Title = "Product with an empty id in the catalog",
                        Severity = AuditSeverity.Fail,
                        Detail = $"'{found.Location}' has {emptyIds.Count} entr(y/ies) in 'products' with no 'id' set.",
                        FixHint = "Every ProductConfig needs a non-empty 'id' (it's the identifier used in code, GetProduct, HasPurchased, etc.)."
                    });
                }

                var duplicateIds = helper.products
                    .Where(p => !string.IsNullOrWhiteSpace(p.id))
                    .GroupBy(p => p.id)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                if (duplicateIds.Count > 0)
                {
                    results.Add(new AuditResult
                    {
                        Category = "IAPHelper",
                        Title = "Duplicate product IDs in the catalog",
                        Severity = AuditSeverity.Warning,
                        Detail = $"'{found.Location}': {string.Join(", ", duplicateIds)}",
                        FixHint = "Remove the duplicate entries in 'products'."
                    });
                }

                results.Add(new AuditResult
                {
                    Category = "IAPHelper",
                    Title = $"autoRestorePurchases = {helper.autoRestorePurchases}",
                    Severity = AuditSeverity.Info,
                    Detail = "Recommended: true on Android/Amazon (silent restore), false on iOS/macOS (requires an explicit button per Apple's policy). " +
                              "If this value is fixed in the Inspector for a multi-platform build, prefer setting it at runtime via IAPHelper.RecommendedAutoRestoreForCurrentPlatform.",
                    FixHint = $"'{found.Location}'"
                });
            }

            return catalogIds;
        }

        private static void AuditFormInstances(List<AuditResult> results, List<FoundComponent<BaseIAPForm>> formInstances)
        {
            if (formInstances.Count == 0)
            {
                results.Add(new AuditResult
                {
                    Category = "BaseIAPForm",
                    Title = "No IAP purchase form found in the project",
                    Severity = AuditSeverity.Warning,
                    Detail = "No BaseIAPForm subclass was found in prefabs/scenes. If the game purchases products without dedicated UI, ignore this warning.",
                    FixHint = null
                });
                return;
            }

            foreach (var found in formInstances)
            {
                if (string.IsNullOrWhiteSpace(found.Component.productId))
                {
                    results.Add(new AuditResult
                    {
                        Category = "BaseIAPForm",
                        Title = "IAP form with no productId configured",
                        Severity = AuditSeverity.Fail,
                        Detail = found.Location,
                        FixHint = "Fill in the 'productId' field in the Inspector with the same id registered in IAPHelper.products."
                    });
                }
            }
        }

        private static void AuditProductIdCrossReference(
            List<AuditResult> results,
            List<FoundComponent<BaseIAPForm>> formInstances,
            HashSet<string> catalogIds)
        {
            foreach (var found in formInstances)
            {
                var productId = found.Component.productId;
                if (string.IsNullOrWhiteSpace(productId))
                    continue; // already reported in AuditFormInstances

                if (!catalogIds.Contains(productId))
                {
                    results.Add(new AuditResult
                    {
                        Category = "Product ID Consistency",
                        Title = $"Form references productId '{productId}' that isn't in any known IAPHelper catalog",
                        Severity = AuditSeverity.Fail,
                        Detail = found.Location,
                        FixHint = "This is the most common cause of the 'bought it but nothing happened' bug class: the form tries to purchase/check an id that IAPHelper.products doesn't know about, so HasPurchased()/GetProduct() never find the product. Make the 'id' in IAPHelper.products and the form's 'productId' identical (case-sensitive)."
                    });
                }
            }

            if (formInstances.Count > 0 && catalogIds.Count > 0)
            {
                var referencedIds = new HashSet<string>(formInstances.Select(f => f.Component.productId).Where(id => !string.IsNullOrWhiteSpace(id)));
                var orphanCatalogIds = catalogIds.Except(referencedIds).ToList();
                if (orphanCatalogIds.Count > 0)
                {
                    results.Add(new AuditResult
                    {
                        Category = "Product ID Consistency",
                        Title = "Product(s) in the catalog with no form referencing them",
                        Severity = AuditSeverity.Info,
                        Detail = string.Join(", ", orphanCatalogIds),
                        FixHint = "Not necessarily an error (the product may be purchased directly via IAPHelper.PurchaseAsync without a BaseIAPForm), but confirm it isn't a forgotten catalog entry."
                    });
                }
            }
        }

        private static void AuditSourceWiring(List<AuditResult> results, List<string> csFiles)
        {
            bool hasFallback = AnyFileMatches(csFiles, @"HasPurchasedFallback\s*=");
            results.Add(new AuditResult
            {
                Category = "Game Save Integration",
                Title = hasFallback ? "HasPurchasedFallback is configured" : "HasPurchasedFallback was not found in source",
                Severity = hasFallback ? AuditSeverity.Pass : AuditSeverity.Warning,
                Detail = hasFallback
                    ? "Found at least one assignment to IAPHelper.HasPurchasedFallback."
                    : "Without this hook, IAPHelper.HasPurchased() can only answer based on the StoreController's cache — if the app opens offline, the local 'already purchased' check can fail even though the game's save data already marks it as unlocked.",
                FixHint = hasFallback ? null : "At game boot: IAPHelper.HasPurchasedFallback = id => SaveData.UnlockedGame && id == \"unlockfullgame\";"
            });

            bool hasEntitlementListener = AnyFileMatches(csFiles, @"OnEntitlementGranted\s*\+=");
            bool hasPurchasesFetchedListener = AnyFileMatches(csFiles, @"OnPurchasesFetched\s*\+=");

            results.Add(new AuditResult
            {
                Category = "Game Save Integration",
                Title = hasEntitlementListener
                    ? "OnEntitlementGranted is being listened to"
                    : (hasPurchasesFetchedListener
                        ? "Only OnPurchasesFetched is being listened to (consider migrating to OnEntitlementGranted)"
                        : "No purchase-grant listener found in source"),
                Severity = hasEntitlementListener ? AuditSeverity.Pass : (hasPurchasesFetchedListener ? AuditSeverity.Warning : AuditSeverity.Fail),
                Detail = hasEntitlementListener
                    ? "Found at least one subscription to IAPHelper.OnEntitlementGranted (permanent event, covers live purchases + restore)."
                    : hasPurchasesFetchedListener
                        ? "OnPurchasesFetched only fires from FetchPurchases() (boot/restore); prefer also subscribing to OnEntitlementGranted, which covers purchases completed live and is the single source of truth for 'product granted'."
                        : "Without either, nothing in the game reacts when IAPHelper grants a product - the game's save data (e.g. SaveData.UnlockedGame) will never be updated.",
                FixHint = hasEntitlementListener ? null : "At game boot: IAPHelper.Instance.OnEntitlementGranted += productId => { if (productId == \"unlockfullgame\") UnlockFullGame(); };"
            });
        }

        private static void AuditPackageVersion(List<AuditResult> results)
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(IAPHelperAudit).Assembly);
            var version = packageInfo?.version ?? "unknown";

            var minFixedVersion = new Version(1, 1, 0);
            var isFixedVersion = Version.TryParse(version, out var parsed) && parsed >= minFixedVersion;

            results.Add(new AuditResult
            {
                Category = "Package Version",
                Title = $"com.wagenheimer.iaphelper {version}",
                Severity = isFixedVersion ? AuditSeverity.Pass : AuditSeverity.Warning,
                Detail = isFixedVersion
                    ? "Includes persistent purchase grant/confirmation (not tied to the purchase UI/timeout) and automatic re-fetch on app resume."
                    : "Versions before 1.1.0 only grant/confirm a purchase through a temporary listener created in PurchaseAsync, which unsubscribes after 60s or when the form closes. " +
                      "If the store's confirmation arrives later than that (app minimized during checkout, pending/deferred payment, etc.), the purchase stays stuck as Pending forever.",
                FixHint = isFixedVersion ? null : "Tools > Wagenheimer > IAP Helper > Check for Updates..."
            });
        }

        #endregion

        #region Project Scanning

        private struct FoundComponent<T> where T : Component
        {
            public T Component;
            public string Location;
        }

        private static List<FoundComponent<T>> FindAllComponents<T>() where T : Component
        {
            var found = new List<FoundComponent<T>>();

            // Prefabs: components can be read directly off the asset, without opening any scene.
            // AssetDatabase.FindAssets already excludes read-only package content unless explicitly asked
            // for, but we scope to "Assets/" defensively too since embedded/local packages can still surface here.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsProjectAssetPath(path))
                    continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                foreach (var component in go.GetComponentsInChildren<T>(true))
                {
                    found.Add(new FoundComponent<T>
                    {
                        Component = component,
                        Location = $"Prefab: {path} ({component.gameObject.name})"
                    });
                }
            }

            // Scenes: opened additively and WITHOUT SAVING, purely to read components. Restricted to scenes
            // under "Assets/" — scenes bundled inside installed packages (Packages/... or resolved under
            // Library/PackageCache/...) are read-only and EditorSceneManager.OpenScene throws
            // "It is not allowed to open a scene in a read-only package" for them, so they're skipped.
            foreach (var scenePath in EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
                         .Concat(FindAllSceneAssetPathsNotInBuildSettings())
                         .Where(IsProjectAssetPath)
                         .Distinct())
            {
                if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
                    continue;

                Scene scene;
                try
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[IAPHelperAudit] Could not open scene '{scenePath}' for auditing: {ex.Message}");
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var component in root.GetComponentsInChildren<T>(true))
                    {
                        found.Add(new FoundComponent<T>
                        {
                            Component = component,
                            Location = $"Scene: {scenePath} ({component.gameObject.name})"
                        });
                    }
                }

                EditorSceneManager.CloseScene(scene, true);
            }

            return found;
        }

        /// <summary>
        /// True for assets that live in the project's own "Assets/" folder — excludes read-only package
        /// content resolved under "Packages/" or "Library/PackageCache/", which cannot be opened/modified
        /// and isn't what this audit is trying to verify anyway (we audit the CONSUMING project's setup).
        /// </summary>
        private static bool IsProjectAssetPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) && assetPath.Replace('\\', '/').StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> FindAllSceneAssetPathsNotInBuildSettings()
        {
            var inBuild = new HashSet<string>(EditorBuildSettings.scenes.Select(s => s.path));
            foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!inBuild.Contains(path))
                    yield return path;
            }
        }

        /// <summary>
        /// Instantiates a throwaway, hidden IAPHelper to read its compiled default 'products' catalog when
        /// no prefab/scene instance exists to inspect. Created and destroyed within a single edit-mode call;
        /// never enters play mode and is never saved.
        /// </summary>
        private static (List<string> ids, string error) TryReadDefaultProductCatalog()
        {
            GameObject go = null;
            try
            {
                go = new GameObject("~IAPHelperAudit_Scratch") { hideFlags = HideFlags.HideAndDontSave };
                // Deactivate BEFORE adding the component: Unity defers Awake()/OnEnable() until a component's
                // GameObject first becomes active, so this prevents IAPHelper.Awake() from running at all
                // (it calls DontDestroyOnLoad, which is invalid outside Play Mode, and would overwrite the
                // real IAPHelper.Instance singleton if this audit is ever run while the game is playing).
                go.SetActive(false);
                var helper = go.AddComponent<IAPHelper>();
                var ids = (helper.products ?? new List<ProductConfig>())
                    .Where(p => !string.IsNullOrWhiteSpace(p.id))
                    .Select(p => p.id)
                    .ToList();
                return (ids, null);
            }
            catch (Exception ex)
            {
                return (new List<string>(), ex.Message);
            }
            finally
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static readonly Regex AddComponentIAPHelperRegex = new Regex(
            @"AddComponent\s*(<\s*IAPHelper\s*>|\(\s*typeof\s*\(\s*IAPHelper\s*\)\s*\))",
            RegexOptions.Compiled);

        private static readonly Regex ProductsOverrideRegex = new Regex(
            @"\.\s*products\s*=(?!=)",
            RegexOptions.Compiled);

        private static List<string> FindRuntimeAddComponentSites(List<string> csFiles)
        {
            return FindMatchingLines(csFiles, AddComponentIAPHelperRegex);
        }

        private static List<string> FindProductsOverrideSites(List<string> csFiles)
        {
            return FindMatchingLines(csFiles, ProductsOverrideRegex);
        }

        private static List<string> FindMatchingLines(List<string> files, Regex regex)
        {
            var hits = new List<string>();
            foreach (var file in files)
            {
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(file);
                }
                catch
                {
                    continue;
                }

                for (int i = 0; i < lines.Length; i++)
                {
                    if (regex.IsMatch(lines[i]))
                        hits.Add($"{ToProjectRelativePath(file)}:{i + 1}");
                }
            }
            return hits;
        }

        private static string ToProjectRelativePath(string absolutePath)
        {
            var dataPath = Application.dataPath.Replace('\\', '/');
            var normalized = absolutePath.Replace('\\', '/');
            if (normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                return "Assets" + normalized.Substring(dataPath.Length);
            return normalized;
        }

        private static List<string> SafeGetAllScripts()
        {
            try
            {
                return Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories).ToList();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[IAPHelperAudit] Failed to scan .cs files: {ex.Message}");
                return new List<string>();
            }
        }

        private static bool AnyFileMatches(List<string> files, string pattern)
        {
            var regex = new Regex(pattern, RegexOptions.Compiled);
            foreach (var file in files)
            {
                string content;
                try
                {
                    content = File.ReadAllText(file);
                }
                catch
                {
                    continue;
                }

                if (regex.IsMatch(content))
                    return true;
            }
            return false;
        }

        #endregion

        #region Report Formatting

        public static string ToMarkdown(List<AuditResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# IAP Helper - Audit Report");
            sb.AppendLine();
            sb.AppendLine($"Generated at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            var fails = results.Count(r => r.Severity == AuditSeverity.Fail);
            var warnings = results.Count(r => r.Severity == AuditSeverity.Warning);
            sb.AppendLine($"**Summary:** {fails} failure(s), {warnings} warning(s), {results.Count} check(s) total.");
            sb.AppendLine();

            foreach (var group in results.GroupBy(r => r.Category))
            {
                sb.AppendLine($"## {group.Key}");
                foreach (var r in group)
                {
                    var icon = r.Severity switch
                    {
                        AuditSeverity.Pass => "✅",
                        AuditSeverity.Info => "ℹ️",
                        AuditSeverity.Warning => "⚠️",
                        AuditSeverity.Fail => "❌",
                        _ => "-"
                    };
                    sb.AppendLine($"- {icon} **{r.Title}**");
                    if (!string.IsNullOrEmpty(r.Detail))
                        sb.AppendLine($"  - {r.Detail}");
                    if (!string.IsNullOrEmpty(r.FixHint))
                        sb.AppendLine($"  - _How to fix:_ {r.FixHint}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        #endregion
    }
}
