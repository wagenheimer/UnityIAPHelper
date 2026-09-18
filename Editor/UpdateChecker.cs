using System;

using UnityEditor;

using UnityEngine;
using UnityEngine.Networking;

namespace Wagenheimer.IAPHelper.Editor
{
    [InitializeOnLoad]
    internal static class UpdateChecker
    {
        const string PackageDisplayName = "IAP Helper";
        internal const string GitUrl = "https://github.com/wagenheimer/UnityIAPHelper.git";
        const string PackageJsonUrl = "https://raw.githubusercontent.com/wagenheimer/UnityIAPHelper/main/package.json";
        const string ChangelogUrl = "https://raw.githubusercontent.com/wagenheimer/UnityIAPHelper/main/CHANGELOG.md";
        const string RepoUrl = "https://github.com/wagenheimer/UnityIAPHelper";
        const string PrefLastCheckTicks = "Wagenheimer.IAPHelper.UpdateChecker.LastCheckTicks";
        const string PrefSkipVersion = "Wagenheimer.IAPHelper.UpdateChecker.SkipVersion";
        const double CheckIntervalHours = 24;

        static UpdateChecker()
        {
            EditorApplication.delayCall += () => CheckForUpdate(force: false);
        }

        [MenuItem("Tools/Wagenheimer/IAP Helper/Check for Updates...", priority = 142)]
        static void CheckForUpdateMenuItem() => CheckForUpdate(force: true);

        internal static void CheckForUpdate(bool force)
        {
            if (!force && !IntervalElapsed())
                return;

            var request = UnityWebRequest.Get(PackageJsonUrl);
            request.timeout = 5;
            var op = request.SendWebRequest();
            op.completed += _ => OnPackageJsonComplete(request, force);
        }

        static bool IntervalElapsed()
        {
            var stored = EditorPrefs.GetString(PrefLastCheckTicks, "0");
            if (!long.TryParse(stored, out var ticks))
                return true;

            return (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalHours >= CheckIntervalHours;
        }

        static void OnPackageJsonComplete(UnityWebRequest request, bool force)
        {
            EditorPrefs.SetString(PrefLastCheckTicks, DateTime.UtcNow.Ticks.ToString());

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log($"[IAPHelper] Update check failed: {request.error}");
                if (force)
                    EditorUtility.DisplayDialog(PackageDisplayName, $"Failed to check for updates:\n{request.error}", "OK");
                request.Dispose();
                return;
            }

            string remoteVersion = null;
            try
            {
                remoteVersion = JsonUtility.FromJson<PackageJsonVersionOnly>(request.downloadHandler.text)?.version;
            }
            catch (Exception e)
            {
                Debug.Log($"[IAPHelper] Update check failed: could not parse remote package.json ({e.Message})");
            }

            request.Dispose();

            var localVersion = GetLocalVersion();
            if (string.IsNullOrEmpty(remoteVersion))
            {
                Debug.Log("[IAPHelper] Update check failed: remote package.json has no version field.");
                if (force)
                    EditorUtility.DisplayDialog(PackageDisplayName, "Failed to check for updates: remote package.json has no version field.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(localVersion))
            {
                Debug.Log("[IAPHelper] Update check failed: could not resolve installed package version.");
                if (force)
                    EditorUtility.DisplayDialog(PackageDisplayName, "Failed to check for updates: could not identify installed package version.", "OK");
                return;
            }

            if (!IsNewer(remoteVersion, localVersion))
            {
                Debug.Log($"[IAPHelper] Up to date (installed: {localVersion}).");
                if (force)
                    EditorUtility.DisplayDialog(PackageDisplayName, $"You are already using the latest version ({localVersion}).", "OK");
                return;
            }

            if (!force && EditorPrefs.GetString(PrefSkipVersion, "") == remoteVersion)
            {
                Debug.Log($"[IAPHelper] Version {remoteVersion} available (installed: {localVersion}) but ignored by user preference.");
                return;
            }

            Debug.Log($"[IAPHelper] New version available: {remoteVersion} (installed: {localVersion}). See {RepoUrl}/releases/latest");
            FetchChangelogAndShow(localVersion, remoteVersion);
        }

        static void FetchChangelogAndShow(string localVersion, string remoteVersion)
        {
            var request = UnityWebRequest.Get(ChangelogUrl);
            request.timeout = 5;
            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                string notes = null;
                if (request.result == UnityWebRequest.Result.Success && request.downloadHandler != null)
                    notes = ExtractVersionNotes(request.downloadHandler.text, remoteVersion, localVersion);

                request.Dispose();
                UpdateAvailableWindow.Show(PackageDisplayName, localVersion, remoteVersion, RepoUrl, GitUrl, notes, PrefSkipVersion);
            };
        }

        static string ExtractVersionNotes(string changelog, string remoteVersion, string localVersion = null)
        {
            if (string.IsNullOrEmpty(changelog))
                return null;

            string CleanVer(string v) => string.IsNullOrEmpty(v) ? "" : v.Trim().TrimStart('v', 'V');
            var cleanRemote = CleanVer(remoteVersion);
            var cleanLocal = CleanVer(localVersion);

            // Possible header markers for the target version
            var candidates = new[]
            {
                $"## [{cleanRemote}]",
                $"## [v{cleanRemote}]",
                $"## {cleanRemote}",
                $"## v{cleanRemote}"
            };

            int start = -1;
            foreach (var c in candidates)
            {
                start = changelog.IndexOf(c, StringComparison.OrdinalIgnoreCase);
                if (start >= 0) break;
            }

            // Fallback: search for first "## [" or "## "
            if (start < 0)
            {
                start = changelog.IndexOf("## [", StringComparison.Ordinal);
                if (start < 0)
                    start = changelog.IndexOf("## ", StringComparison.Ordinal);
            }

            if (start < 0)
                return changelog.Length > 800 ? changelog.Substring(0, 800) + "..." : changelog;

            // Find where this release section starts (after the ## line)
            var bodyStart = changelog.IndexOf('\n', start);
            if (bodyStart < 0) bodyStart = start;

            // If we know localVersion, try to include all changes up to localVersion!
            int end = -1;
            if (!string.IsNullOrEmpty(cleanLocal) && cleanLocal != cleanRemote)
            {
                var localCandidates = new[]
                {
                    $"## [{cleanLocal}]",
                    $"## [v{cleanLocal}]",
                    $"## {cleanLocal}",
                    $"## v{cleanLocal}"
                };

                foreach (var lc in localCandidates)
                {
                    end = changelog.IndexOf(lc, bodyStart, StringComparison.OrdinalIgnoreCase);
                    if (end >= 0) break;
                }
            }

            // If localVersion not found or not given, find the next section boundary
            if (end < 0)
            {
                end = changelog.IndexOf("\n## [", bodyStart, StringComparison.Ordinal);
                if (end < 0) end = changelog.IndexOf("\n## ", bodyStart, StringComparison.Ordinal);
            }

            var length = (end >= 0 ? end : changelog.Length) - bodyStart;
            if (length <= 0) return null;

            return changelog.Substring(bodyStart, length).Trim();
        }

        static string GetLocalVersion()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UpdateChecker).Assembly);
            return packageInfo?.version;
        }

        static bool IsNewer(string remote, string local)
        {
            if (Version.TryParse(remote, out var remoteVer) && Version.TryParse(local, out var localVer))
                return remoteVer > localVer;

            return string.CompareOrdinal(remote, local) > 0;
        }

        [Serializable]
        class PackageJsonVersionOnly
        {
            public string version;
        }
    }
}
