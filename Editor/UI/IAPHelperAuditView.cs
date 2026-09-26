using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.IAPHelper.Editor
{
    internal sealed class IAPHelperAuditView
    {
        public VisualElement Root { get; }

        private List<AuditResult> _results;
        private AuditSeverity? _severityFilter;
        private string _searchFilter = "";
        private VisualElement _resultsContainer;
        private Label _summaryLabel;

        public IAPHelperAuditView()
        {
            Root = new VisualElement();
            IAPHelperUIStyle.Apply(Root);

            BuildUI();
        }

        private void BuildUI()
        {
            var headerCard = IAPHelperUIStyle.CreateCard("🔍 Project Setup Verification Audit", "Automated scan of scene components, package settings, and product wiring.");

            var actionsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8 } };

            var runBtn = new Button(RunAudit) { text = "▶ Run Audit Now" };
            runBtn.AddToClassList("iap-toolbar-btn");
            runBtn.style.backgroundColor = new Color(0.18f, 0.42f, 0.75f);
            runBtn.style.color = Color.white;
            runBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            actionsRow.Add(runBtn);

            var filterAllBtn = new Button(() => SetFilter(null)) { text = "All" };
            filterAllBtn.AddToClassList("iap-toolbar-btn");
            actionsRow.Add(filterAllBtn);

            var filterFailBtn = new Button(() => SetFilter(AuditSeverity.Fail)) { text = "✕ Fails Only" };
            filterFailBtn.AddToClassList("iap-toolbar-btn");
            actionsRow.Add(filterFailBtn);

            var filterWarnBtn = new Button(() => SetFilter(AuditSeverity.Warning)) { text = "⚠ Warnings Only" };
            filterWarnBtn.AddToClassList("iap-toolbar-btn");
            actionsRow.Add(filterWarnBtn);

            headerCard.Add(actionsRow);

            var copyRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8 } };

            var copyReportBtn = CreateCopyButton("📋 Copy Report", "Copy the full audit as Markdown.", () =>
                _results != null ? IAPHelperAudit.ToMarkdown(_results) : null);
            copyRow.Add(copyReportBtn);

            var copyPromptBtn = CreateCopyButton("🤖 Copy AI Fix Prompt (all)", "Copy one ready-to-paste prompt that makes an AI agent fix every warning and failure.", () =>
                _results != null ? IAPHelperAudit.ToPromptMarkdown(_results) : null);
            copyPromptBtn.style.backgroundColor = new Color(0.18f, 0.42f, 0.75f);
            copyPromptBtn.style.color = Color.white;
            copyPromptBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            copyRow.Add(copyPromptBtn);

            headerCard.Add(copyRow);

            _summaryLabel = new Label("Click 'Run Audit Now' to scan your project for potential store submission issues.");
            _summaryLabel.style.fontSize = 11;
            _summaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _summaryLabel.style.color = new Color(0.85f, 0.85f, 0.90f);
            headerCard.Add(_summaryLabel);

            Root.Add(headerCard);

            _resultsContainer = new VisualElement();
            Root.Add(_resultsContainer);
        }

        public void RunAudit()
        {
            _results = IAPHelperAudit.RunAudit();
            RefreshResults();
        }

        private const long CopiedFeedbackMs = 1500;

        /// <summary>
        /// Button that copies <paramref name="getText"/>() to the clipboard and briefly shows "Copied".
        /// Shows a hint instead when there is nothing to copy (audit not run yet).
        /// </summary>
        private static Button CreateCopyButton(string label, string tooltip, Func<string> getText)
        {
            var button = new Button { text = label, tooltip = tooltip };
            button.AddToClassList("iap-toolbar-btn");
            button.clicked += () =>
            {
                var text = getText();
                button.text = string.IsNullOrEmpty(text) ? "Run the audit first" : "✓ Copied";
                if (!string.IsNullOrEmpty(text))
                    GUIUtility.systemCopyBuffer = text;

                button.schedule.Execute(() => button.text = label).ExecuteLater(CopiedFeedbackMs);
            };
            return button;
        }

        private static string FormatFinding(AuditResult r)
        {
            var text = $"[{r.Severity}] {r.Category} - {r.Title}";
            if (!string.IsNullOrEmpty(r.Detail))
                text += "\n" + r.Detail;
            if (!string.IsNullOrEmpty(r.FixHint))
                text += "\nHow to fix: " + r.FixHint;
            return text;
        }

        private void SetFilter(AuditSeverity? filter)
        {
            _severityFilter = filter;
            RefreshResults();
        }

        private void RefreshResults()
        {
            _resultsContainer.Clear();

            if (_results == null || _results.Count == 0)
            {
                _resultsContainer.Add(IAPHelperUIStyle.CreateCallout("No audit results to display. Click 'Run Audit Now' to execute automated checks.", "info"));
                return;
            }

            int fails = _results.Count(r => r.Severity == AuditSeverity.Fail);
            int warnings = _results.Count(r => r.Severity == AuditSeverity.Warning);
            int passes = _results.Count(r => r.Severity == AuditSeverity.Pass);

            if (fails > 0)
            {
                _summaryLabel.text = $"❌ {fails} critical failure(s) found — resolve before publishing.";
                _summaryLabel.style.color = new Color(0.95f, 0.35f, 0.35f);
            }
            else if (warnings > 0)
            {
                _summaryLabel.text = $"⚠️ {warnings} warning(s) found — review recommended.";
                _summaryLabel.style.color = new Color(0.95f, 0.70f, 0.25f);
            }
            else
            {
                _summaryLabel.text = $"✓ All {passes} automated checks passed cleanly!";
                _summaryLabel.style.color = new Color(0.35f, 0.85f, 0.45f);
            }

            var filtered = _results
                .Where(r => !_severityFilter.HasValue || r.Severity == _severityFilter.Value)
                .ToList();

            var groups = filtered.GroupBy(r => r.Category);
            foreach (var group in groups)
            {
                var card = IAPHelperUIStyle.CreateCard(group.Key);

                foreach (var item in group)
                {
                    var itemRow = new VisualElement();
                    itemRow.style.flexDirection = FlexDirection.Row;
                    itemRow.style.justifyContent = Justify.SpaceBetween;
                    itemRow.style.alignItems = Align.FlexStart;
                    itemRow.style.paddingTop = 6;
                    itemRow.style.paddingBottom = 6;
                    itemRow.style.borderBottomWidth = 1;
                    itemRow.style.borderBottomColor = new Color(0.25f, 0.25f, 0.28f);

                    var textCol = new VisualElement { style = { flexGrow = 1, marginRight = 10 } };
                    var title = new Label(item.Title);
                    title.style.fontSize = 12;
                    title.style.unityFontStyleAndWeight = FontStyle.Bold;
                    textCol.Add(title);

                    if (!string.IsNullOrEmpty(item.Detail))
                    {
                        var desc = new Label(item.Detail);
                        desc.style.fontSize = 10;
                        desc.style.color = new Color(0.70f, 0.70f, 0.75f);
                        desc.style.whiteSpace = WhiteSpace.Normal;
                        desc.style.marginTop = 2;
                        textCol.Add(desc);
                    }

                    if (!string.IsNullOrEmpty(item.FixHint))
                    {
                        var hint = new Label("💡 " + item.FixHint);
                        hint.style.fontSize = 10;
                        hint.style.color = new Color(0.45f, 0.75f, 0.95f);
                        hint.style.whiteSpace = WhiteSpace.Normal;
                        hint.style.marginTop = 2;
                        textCol.Add(hint);
                    }

                    itemRow.Add(textCol);

                    var itemCopy = item;
                    var copyBtn = CreateCopyButton("📋", "Copy this finding.", () => FormatFinding(itemCopy));
                    copyBtn.style.marginRight = 4;
                    itemRow.Add(copyBtn);

                    if (!string.IsNullOrEmpty(item.Prompt))
                    {
                        var promptBtn = CreateCopyButton("🤖", "Copy an AI prompt that fixes this finding.", () => itemCopy.Prompt);
                        promptBtn.style.marginRight = 6;
                        itemRow.Add(promptBtn);
                    }

                    var badge = IAPHelperUIStyle.CreateBadge(item.Severity.ToString(), item.Severity.ToString().ToLower());
                    itemRow.Add(badge);

                    card.Add(itemRow);
                }

                _resultsContainer.Add(card);
            }
        }
    }
}
