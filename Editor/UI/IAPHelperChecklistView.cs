using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.IAPHelper.Editor
{
    internal sealed class IAPHelperChecklistView
    {
        public VisualElement Root { get; }

        private const string PrefKeyPrefix = "iaphelper_chk_";

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

        private Label _progressLabel;

        public IAPHelperChecklistView()
        {
            Root = new VisualElement();
            IAPHelperUIStyle.Apply(Root);

            BuildUI();
        }

        private void BuildUI()
        {
            var headerCard = IAPHelperUIStyle.CreateCard("📋 Pre-Release Store Submission Checklist", "Track manual store console requirements across Google Play Console and Apple App Store Connect.");

            var topRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center, marginBottom = 8 } };

            _progressLabel = new Label();
            _progressLabel.style.fontSize = 11;
            _progressLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _progressLabel.style.color = new Color(0.35f, 0.85f, 0.45f);
            topRow.Add(_progressLabel);

            var resetBtn = new Button(ResetChecklist) { text = "↺ Reset All" };
            resetBtn.AddToClassList("iap-toolbar-btn");
            topRow.Add(resetBtn);

            headerCard.Add(topRow);
            Root.Add(headerCard);

            var groups = StoreChecklistItems.GroupBy(i => i.Category);

            foreach (var group in groups)
            {
                var card = IAPHelperUIStyle.CreateCard(group.Key);

                foreach (var item in group)
                {
                    var itemElement = new VisualElement();
                    itemElement.AddToClassList("iap-checklist-item");

                    bool isChecked = EditorPrefs.GetBool(PrefKeyPrefix + item.Id, false);
                    if (isChecked)
                    {
                        itemElement.AddToClassList("checked");
                    }

                    var toggle = new Toggle();
                    toggle.value = isChecked;
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        EditorPrefs.SetBool(PrefKeyPrefix + item.Id, evt.newValue);
                        if (evt.newValue)
                            itemElement.AddToClassList("checked");
                        else
                            itemElement.RemoveFromClassList("checked");

                        UpdateProgress();
                    });
                    itemElement.Add(toggle);

                    var textCol = new VisualElement();
                    textCol.AddToClassList("iap-checklist-text");

                    var title = new Label(item.Label);
                    title.AddToClassList("iap-checklist-title");
                    textCol.Add(title);

                    var desc = new Label(item.Description);
                    desc.AddToClassList("iap-checklist-desc");
                    desc.style.whiteSpace = WhiteSpace.Normal;
                    textCol.Add(desc);

                    itemElement.Add(textCol);
                    card.Add(itemElement);
                }

                Root.Add(card);
            }

            UpdateProgress();
        }

        private void UpdateProgress()
        {
            int total = StoreChecklistItems.Length;
            int checkedCount = StoreChecklistItems.Count(i => EditorPrefs.GetBool(PrefKeyPrefix + i.Id, false));
            _progressLabel.text = $"Progress: {checkedCount} / {total} verified ({checkedCount * 100 / total}%)";
        }

        private void ResetChecklist()
        {
            if (EditorUtility.DisplayDialog("Reset Checklist", "Reset all checklist items to unchecked?", "RESET", "CANCEL"))
            {
                foreach (var item in StoreChecklistItems)
                {
                    EditorPrefs.DeleteKey(PrefKeyPrefix + item.Id);
                }
                Root.Clear();
                BuildUI();
            }
        }
    }
}
