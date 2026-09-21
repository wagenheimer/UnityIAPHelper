using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

namespace Wagenheimer.IAPHelper.Editor
{
    internal class IAPHelperAuditWindow : EditorWindow
    {
        private List<AuditResult> _results = new List<AuditResult>();
        private Vector2 _scroll;

        private static readonly Color PassColor = new Color(0.24f, 0.72f, 0.35f);
        private static readonly Color InfoColor = new Color(0.45f, 0.55f, 0.95f);
        private static readonly Color WarningColor = new Color(0.85f, 0.65f, 0.13f);
        private static readonly Color FailColor = new Color(0.82f, 0.25f, 0.25f);

        public static void ShowWindow(List<AuditResult> results)
        {
            var window = GetWindow<IAPHelperAuditWindow>(true, "IAP Helper - Setup Verification");
            window._results = results;
            window.minSize = new Vector2(520, 420);
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();

            GUILayout.Space(4);

            var fails = 0;
            var warnings = 0;
            foreach (var r in _results)
            {
                if (r.Severity == AuditSeverity.Fail) fails++;
                else if (r.Severity == AuditSeverity.Warning) warnings++;
            }

            var summaryType = fails > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info;
            var summaryText = fails > 0
                ? $"{fails} critical failure(s) found - resolve before publishing the build."
                : warnings > 0
                    ? $"{warnings} warning(s) - worth reviewing."
                    : "No critical failures found in the automated checks.";
            EditorGUILayout.HelpBox(summaryText, summaryType);

            EditorGUILayout.HelpBox(
                "This only covers what can be verified from code/assets. Items that only exist in the store consoles " +
                "(Google Play / App Store) still need manual checking - see IAP-CHECKLIST.md in the package.",
                MessageType.None);

            GUILayout.Space(6);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            string currentCategory = null;
            foreach (var r in _results)
            {
                if (r.Category != currentCategory)
                {
                    currentCategory = r.Category;
                    GUILayout.Space(8);
                    EditorGUILayout.LabelField(currentCategory, EditorStyles.boldLabel);
                }

                DrawResult(r);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Run again", EditorStyles.toolbarButton, GUILayout.Width(120)))
                _results = IAPHelperAudit.RunAudit();

            if (GUILayout.Button("Copy report (Markdown)", EditorStyles.toolbarButton, GUILayout.Width(180)))
            {
                EditorGUIUtility.systemCopyBuffer = IAPHelperAudit.ToMarkdown(_results);
                ShowNotification(new GUIContent("Report copied!"));
            }

            int pendingPrompts = 0;
            foreach (var r in _results)
                if (!string.IsNullOrEmpty(r.Prompt))
                    pendingPrompts++;

            using (new EditorGUI.DisabledScope(pendingPrompts == 0))
            {
                if (GUILayout.Button($"Copy AI prompts ({pendingPrompts})", EditorStyles.toolbarButton, GUILayout.Width(170)))
                {
                    EditorGUIUtility.systemCopyBuffer = IAPHelperAudit.ToPromptMarkdown(_results);
                    ShowNotification(new GUIContent("AI prompts copied!"));
                }
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawResult(AuditResult r)
        {
            var color = r.Severity switch
            {
                AuditSeverity.Pass => PassColor,
                AuditSeverity.Info => InfoColor,
                AuditSeverity.Warning => WarningColor,
                AuditSeverity.Fail => FailColor,
                _ => Color.gray
            };

            var icon = r.Severity switch
            {
                AuditSeverity.Pass => "✓",
                AuditSeverity.Info => "i",
                AuditSeverity.Warning => "!",
                AuditSeverity.Fail => "✕",
                _ => "-"
            };

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var prevColor = GUI.color;
            GUI.color = color;
            GUILayout.Label(icon, GUILayout.Width(18));
            GUI.color = prevColor;

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(r.Title, EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(r.Detail))
                EditorGUILayout.LabelField(r.Detail, EditorStyles.wordWrappedMiniLabel);

            if (!string.IsNullOrEmpty(r.FixHint))
            {
                var prev = GUI.contentColor;
                GUI.contentColor = new Color(0.5f, 0.75f, 1f);
                EditorGUILayout.LabelField("How to fix: " + r.FixHint, EditorStyles.wordWrappedMiniLabel);
                GUI.contentColor = prev;
            }

            if (!string.IsNullOrEmpty(r.Prompt))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Copy AI prompt", GUILayout.Width(120)))
                {
                    EditorGUIUtility.systemCopyBuffer = r.Prompt;
                    ShowNotification(new GUIContent("AI prompt copied"));
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
    }
}
