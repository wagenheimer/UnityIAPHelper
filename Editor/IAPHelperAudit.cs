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
    /// (prefabs, scenes, and C# source) for the configuration mistakes that most commonly cause the
    /// "bought the game but ads/gates never lift" class of bug: mismatched product IDs between
    /// <see cref="IAPHelper"/> and <see cref="BaseIAPForm"/>, missing catalog entries, no local
    /// fallback/restore wiring, or an outdated package version that lacks the persistent
    /// grant/confirm fix.
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

            AuditHelperInstances(results, helperInstances);
            AuditFormInstances(results, formInstances);
            AuditProductIdCrossReference(results, helperInstances, formInstances);
            AuditSourceWiring(results);
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
                    Title = "Não foi possível listar os pacotes instalados",
                    Severity = AuditSeverity.Warning,
                    Detail = listRequest.Error?.message ?? "Erro desconhecido ao consultar o Package Manager.",
                    FixHint = "Rode novamente com o Package Manager ocioso (fora de uma operação de add/remove)."
                });
                return;
            }

            var purchasing = listRequest.Result.FirstOrDefault(p => p.name == "com.unity.purchasing");
            if (purchasing == null)
            {
                results.Add(new AuditResult
                {
                    Category = "Unity Purchasing",
                    Title = "com.unity.purchasing não está instalado",
                    Severity = AuditSeverity.Fail,
                    Detail = "IAPHelper depende do pacote oficial Unity IAP v5+.",
                    FixHint = "Window > Package Manager > Unity Registry > In App Purchasing, instale a versão 5.4.3 ou superior."
                });
                return;
            }

            var minVersion = new Version(5, 4, 3);
            var installedVersionOk = Version.TryParse(purchasing.version, out var installedVersion) && installedVersion >= minVersion;

            results.Add(new AuditResult
            {
                Category = "Unity Purchasing",
                Title = $"com.unity.purchasing {purchasing.version} instalado",
                Severity = installedVersionOk ? AuditSeverity.Pass : AuditSeverity.Fail,
                Detail = installedVersionOk
                    ? "Versão compatível com o fluxo Pending -> Confirm (StoreController v5)."
                    : $"Versão {purchasing.version} é anterior à mínima suportada (5.4.3). A API v5 (StoreController, Orders, PendingOrder) pode não existir nessa versão.",
                FixHint = installedVersionOk ? null : "Atualize com Window > Package Manager > In App Purchasing > Update."
            });
        }

        private static void AuditHelperInstances(List<AuditResult> results, List<FoundComponent<IAPHelper>> helperInstances)
        {
            if (helperInstances.Count == 0)
            {
                results.Add(new AuditResult
                {
                    Category = "IAPHelper",
                    Title = "Nenhum componente IAPHelper encontrado no projeto",
                    Severity = AuditSeverity.Fail,
                    Detail = "Nem em prefabs nem em cenas foi localizado um GameObject com o componente IAPHelper.",
                    FixHint = "Adicione IAPHelper.Instance na inicialização do jogo (ex: AddComponent no boot) ou coloque o componente em um GameObject persistente (DontDestroyOnLoad já é aplicado internamente)."
                });
            }
            else if (helperInstances.Count > 1)
            {
                results.Add(new AuditResult
                {
                    Category = "IAPHelper",
                    Title = $"{helperInstances.Count} componentes IAPHelper encontrados",
                    Severity = AuditSeverity.Warning,
                    Detail = "IAPHelper é um singleton (DontDestroyOnLoad); múltiplas instâncias em prefabs/cenas diferentes só é seguro se apenas uma delas for instanciada em runtime (as demais são destruídas em Awake). Verifique se isso é intencional.",
                    FixHint = string.Join("\n", helperInstances.Select(h => $"- {h.Location}"))
                });
            }
            else
            {
                results.Add(new AuditResult
                {
                    Category = "IAPHelper",
                    Title = "1 componente IAPHelper encontrado",
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
                        Title = "Catálogo de produtos vazio",
                        Severity = AuditSeverity.Fail,
                        Detail = $"'{found.Location}' não tem nenhum produto configurado na lista 'products'.",
                        FixHint = "Preencha 'products' com pelo menos um ProductConfig cujo 'id' bata com o SKU da loja."
                    });
                    continue;
                }

                var emptyIds = helper.products.Where(p => string.IsNullOrWhiteSpace(p.id)).ToList();
                if (emptyIds.Count > 0)
                {
                    results.Add(new AuditResult
                    {
                        Category = "IAPHelper",
                        Title = "Produto com id vazio no catálogo",
                        Severity = AuditSeverity.Fail,
                        Detail = $"'{found.Location}' tem {emptyIds.Count} entrada(s) em 'products' sem 'id' preenchido.",
                        FixHint = "Todo ProductConfig precisa de um 'id' não-vazio (é o identificador usado em código, GetProduct, HasPurchased, etc.)."
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
                        Title = "IDs de produto duplicados no catálogo",
                        Severity = AuditSeverity.Warning,
                        Detail = $"'{found.Location}': {string.Join(", ", duplicateIds)}",
                        FixHint = "Remova as entradas duplicadas em 'products'."
                    });
                }

                results.Add(new AuditResult
                {
                    Category = "IAPHelper",
                    Title = $"autoRestorePurchases = {helper.autoRestorePurchases}",
                    Severity = AuditSeverity.Info,
                    Detail = "Recomendado: true no Android/Amazon (restauração silenciosa), false no iOS/macOS (exige botão explícito por política da Apple). " +
                              "Se este valor é fixo no Inspector para uma build multi-plataforma, prefira setá-lo em runtime via IAPHelper.RecommendedAutoRestoreForCurrentPlatform.",
                    FixHint = $"'{found.Location}'"
                });
            }
        }

        private static void AuditFormInstances(List<AuditResult> results, List<FoundComponent<BaseIAPForm>> formInstances)
        {
            if (formInstances.Count == 0)
            {
                results.Add(new AuditResult
                {
                    Category = "BaseIAPForm",
                    Title = "Nenhum form de IAP encontrado no projeto",
                    Severity = AuditSeverity.Warning,
                    Detail = "Nenhuma subclasse de BaseIAPForm foi localizada em prefabs/cenas. Se o jogo compra produtos sem UI dedicada, ignore este aviso.",
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
                        Title = "Form de IAP sem productId configurado",
                        Severity = AuditSeverity.Fail,
                        Detail = found.Location,
                        FixHint = "Preencha o campo 'productId' no Inspector com o mesmo id cadastrado em IAPHelper.products."
                    });
                }
            }
        }

        private static void AuditProductIdCrossReference(
            List<AuditResult> results,
            List<FoundComponent<IAPHelper>> helperInstances,
            List<FoundComponent<BaseIAPForm>> formInstances)
        {
            var catalogIds = new HashSet<string>(
                helperInstances
                    .Where(h => h.Component.products != null)
                    .SelectMany(h => h.Component.products)
                    .Select(p => p.id)
                    .Where(id => !string.IsNullOrWhiteSpace(id)));

            foreach (var found in formInstances)
            {
                var productId = found.Component.productId;
                if (string.IsNullOrWhiteSpace(productId))
                    continue; // já reportado em AuditFormInstances

                if (!catalogIds.Contains(productId))
                {
                    results.Add(new AuditResult
                    {
                        Category = "Consistência de Product ID",
                        Title = $"Form referencia productId '{productId}' que não existe em nenhum catálogo IAPHelper",
                        Severity = AuditSeverity.Fail,
                        Detail = found.Location,
                        FixHint = "Este é o bug mais comum da classe 'comprou mas nada acontece': o form tenta comprar/checar um id que o IAPHelper.products não conhece, " +
                                  "então HasPurchased()/GetProduct() nunca encontram o produto. Corrija o 'id' em IAPHelper.products ou o 'productId' do form para que sejam idênticos (case-sensitive)."
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
                        Category = "Consistência de Product ID",
                        Title = "Produto(s) no catálogo sem nenhum form referenciando",
                        Severity = AuditSeverity.Info,
                        Detail = string.Join(", ", orphanCatalogIds),
                        FixHint = "Não é necessariamente um erro (o produto pode ser comprado via IAPHelper.PurchaseAsync diretamente, sem BaseIAPForm), mas confirme que não é um catálogo esquecido."
                    });
                }
            }
        }

        private static void AuditSourceWiring(List<AuditResult> results)
        {
            var csFiles = SafeGetAllScripts();

            bool hasFallback = AnyFileMatches(csFiles, @"HasPurchasedFallback\s*=");
            results.Add(new AuditResult
            {
                Category = "Integração com o Save do jogo",
                Title = hasFallback ? "HasPurchasedFallback está configurado" : "HasPurchasedFallback não foi encontrado no código-fonte",
                Severity = hasFallback ? AuditSeverity.Pass : AuditSeverity.Warning,
                Detail = hasFallback
                    ? "Encontrada ao menos uma atribuição a IAPHelper.HasPurchasedFallback."
                    : "Sem esse hook, IAPHelper.HasPurchased() só sabe responder com base no cache do StoreController — se o app abrir offline, a checagem local de 'já comprei' pode falhar mesmo com o save do jogo já marcado como desbloqueado.",
                FixHint = hasFallback ? null : "No boot do jogo: IAPHelper.HasPurchasedFallback = id => SaveData.UnlockedGame && id == \"unlockfullgame\";"
            });

            bool hasEntitlementListener = AnyFileMatches(csFiles, @"OnEntitlementGranted\s*\+=");
            bool hasPurchasesFetchedListener = AnyFileMatches(csFiles, @"OnPurchasesFetched\s*\+=");

            results.Add(new AuditResult
            {
                Category = "Integração com o Save do jogo",
                Title = hasEntitlementListener
                    ? "OnEntitlementGranted está sendo escutado"
                    : (hasPurchasesFetchedListener
                        ? "Apenas OnPurchasesFetched está sendo escutado (considere migrar para OnEntitlementGranted)"
                        : "Nenhum listener de concessão de compra encontrado no código-fonte"),
                Severity = hasEntitlementListener ? AuditSeverity.Pass : (hasPurchasesFetchedListener ? AuditSeverity.Warning : AuditSeverity.Fail),
                Detail = hasEntitlementListener
                    ? "Encontrada ao menos uma assinatura de IAPHelper.OnEntitlementGranted (evento permanente, cobre compra ao vivo + restauração)."
                    : hasPurchasesFetchedListener
                        ? "OnPurchasesFetched só dispara em FetchPurchases() (boot/restore); prefira também assinar OnEntitlementGranted, que cobre compras concluídas ao vivo e é a fonte única de verdade para 'produto concedido'."
                        : "Sem nenhum dos dois, nada no jogo reage quando o IAPHelper concede um produto - o save do jogo (ex: SaveData.UnlockedGame) nunca será atualizado.",
                FixHint = hasEntitlementListener ? null : "No boot do jogo: IAPHelper.Instance.OnEntitlementGranted += productId => { if (productId == \"unlockfullgame\") UnlockFullGame(); };"
            });
        }

        private static void AuditPackageVersion(List<AuditResult> results)
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(IAPHelperAudit).Assembly);
            var version = packageInfo?.version ?? "desconhecida";

            var minFixedVersion = new Version(1, 1, 0);
            var isFixedVersion = Version.TryParse(version, out var parsed) && parsed >= minFixedVersion;

            results.Add(new AuditResult
            {
                Category = "Versão do pacote",
                Title = $"com.wagenheimer.iaphelper {version}",
                Severity = isFixedVersion ? AuditSeverity.Pass : AuditSeverity.Warning,
                Detail = isFixedVersion
                    ? "Inclui a concessão/confirmação de compra persistente (não depende da UI/timeout do fluxo de compra) e a reconsulta automática ao retomar o foco do app."
                    : "Versões anteriores a 1.1.0 só concedem/confirmam a compra através de um listener temporário criado em PurchaseAsync, que se desinscreve após 60s ou ao fechar o form. " +
                      "Se a confirmação da loja chegar depois disso (app minimizado durante o checkout, pagamento pendente/diferido, etc.), a compra fica presa como Pending para sempre.",
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

            // Prefabs: componentes podem ser lidos diretamente do asset, sem abrir nenhuma cena.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
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

            // Cenas: abertas aditivamente e SEM SALVAR, apenas para leitura dos componentes. É o mesmo
            // padrão usado por ferramentas de auditoria/build de projeto Unity.
            var originalScenes = GetOpenScenePaths();
            foreach (var scenePath in EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
                         .Concat(FindAllSceneAssetPathsNotInBuildSettings()))
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
                    Debug.LogWarning($"[IAPHelperAudit] Não foi possível abrir a cena '{scenePath}' para auditoria: {ex.Message}");
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var component in root.GetComponentsInChildren<T>(true))
                    {
                        found.Add(new FoundComponent<T>
                        {
                            Component = component,
                            Location = $"Cena: {scenePath} ({component.gameObject.name})"
                        });
                    }
                }

                EditorSceneManager.CloseScene(scene, true);
            }

            RestoreOpenScenes(originalScenes);

            return found;
        }

        private static string[] GetOpenScenePaths()
        {
            var paths = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!string.IsNullOrEmpty(scene.path))
                    paths.Add(scene.path);
            }
            return paths.ToArray();
        }

        private static void RestoreOpenScenes(string[] originalScenes)
        {
            // Best-effort apenas: não força reabertura para não descartar mudanças não salvas do usuário
            // caso a auditoria tenha sido chamada com cenas modificadas abertas. As cenas abertas ADITIVAMENTE
            // por esta auditoria já foram fechadas sem salvar logo após a leitura.
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

        private static List<string> SafeGetAllScripts()
        {
            try
            {
                return Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories).ToList();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[IAPHelperAudit] Falha ao varrer arquivos .cs: {ex.Message}");
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
            sb.AppendLine("# IAP Helper - Relatório de Auditoria");
            sb.AppendLine();
            sb.AppendLine($"Gerado em {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            var fails = results.Count(r => r.Severity == AuditSeverity.Fail);
            var warnings = results.Count(r => r.Severity == AuditSeverity.Warning);
            sb.AppendLine($"**Resumo:** {fails} falha(s), {warnings} aviso(s), {results.Count} verificação(ões) no total.");
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
                        sb.AppendLine($"  - _Como corrigir:_ {r.FixHint}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        #endregion
    }
}
