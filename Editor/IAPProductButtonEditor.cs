using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Wagenheimer.IAPHelper.UI;

namespace Wagenheimer.IAPHelper.Editor
{
    /// <summary>
    /// Custom UI Toolkit Inspector for <see cref="IAPProductButton"/>.
    /// Provides product catalog ID dropdown, binding status check badges, and 1-click Auto-Resolve.
    /// </summary>
    [CustomEditor(typeof(IAPProductButton), true)]
    public class IAPProductButtonEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            IAPHelperUIStyle.Apply(root);

            var comp = (IAPProductButton)target;

            // 1. Product Selection Card
            var prodCard = IAPHelperUIStyle.CreateCard("🎯 Product Selection", "Assign the catalog Product ID to bind to this purchase button.");

            var availableIds = GetAvailableCatalogProductIds();
            var idProp = serializedObject.FindProperty("productId");

            if (availableIds.Count > 0)
            {
                int currentIndex = availableIds.IndexOf(idProp.stringValue);
                var options = new List<string>(availableIds);
                options.Add("[Custom ID...]");

                int selectedIdx = currentIndex >= 0 ? currentIndex : options.Count - 1;
                var dropdown = new PopupField<string>("Catalog Product", options, selectedIdx);
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue != "[Custom ID...]")
                    {
                        idProp.stringValue = evt.newValue;
                        serializedObject.ApplyModifiedProperties();
                    }
                });
                prodCard.Add(dropdown);
            }

            prodCard.Add(new PropertyField(idProp, "Product ID"));
            root.Add(prodCard);

            // 2. UI Bindings Card
            var bindCard = IAPHelperUIStyle.CreateCard("🔗 UI Component Bindings", "Visual elements dynamically updated with catalog price, title, and owned state.");

            var resolveBtn = new Button(() =>
            {
                comp.AutoResolveComponents();
                EditorUtility.SetDirty(comp);
                serializedObject.Update();
            })
            { text = "✨ Auto-Resolve Components" };
            resolveBtn.AddToClassList("iap-toolbar-btn");
            resolveBtn.style.alignSelf = Align.FlexStart;
            resolveBtn.style.marginBottom = 8;
            bindCard.Add(resolveBtn);

            // Status Badges Row
            var statusBox = new VisualElement { style = { marginBottom = 8 } };
            statusBox.Add(CreateBindingStatusRow("Buy Button (Required)", comp.buttonBuy != null, true));
            statusBox.Add(CreateBindingStatusRow("Price Label (Required)", comp.labelPrice != null, true));
            statusBox.Add(CreateBindingStatusRow("Title Label (Optional)", comp.labelTitle != null, false));
            statusBox.Add(CreateBindingStatusRow("Loading Indicator (Optional)", comp.loadingIndicator != null, false));
            statusBox.Add(CreateBindingStatusRow("Owned Badge (Optional)", comp.ownedBadge != null, false));
            bindCard.Add(statusBox);

            bindCard.Add(new PropertyField(serializedObject.FindProperty("buttonBuy")));
            bindCard.Add(new PropertyField(serializedObject.FindProperty("labelPrice")));
            bindCard.Add(new PropertyField(serializedObject.FindProperty("labelTitle")));
            bindCard.Add(new PropertyField(serializedObject.FindProperty("labelDescription")));
            bindCard.Add(new PropertyField(serializedObject.FindProperty("imageIcon")));
            bindCard.Add(new PropertyField(serializedObject.FindProperty("ownedBadge")));
            bindCard.Add(new PropertyField(serializedObject.FindProperty("loadingIndicator")));
            root.Add(bindCard);

            // 3. Owned State Behavior Card
            var ownedCard = IAPHelperUIStyle.CreateCard("🛡️ Owned State Behavior", "How the UI changes once the product is acquired.");
            ownedCard.Add(new PropertyField(serializedObject.FindProperty("ownedStateBehavior")));
            ownedCard.Add(new PropertyField(serializedObject.FindProperty("ownedText")));
            root.Add(ownedCard);

            // 4. Events Card
            var eventsCard = IAPHelperUIStyle.CreateCard("⚡ Button Events");
            eventsCard.Add(new PropertyField(serializedObject.FindProperty("onPurchaseSuccess")));
            eventsCard.Add(new PropertyField(serializedObject.FindProperty("onPurchaseFailed")));
            root.Add(eventsCard);

            return root;
        }

        private VisualElement CreateBindingStatusRow(string label, bool isBound, bool required)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center, marginBottom = 2 } };
            var text = new Label(label) { style = { fontSize = 11 } };
            row.Add(text);

            string badgeText = isBound ? "✓ Bound" : (required ? "✕ Missing" : "– Unset");
            string badgeSeverity = isBound ? "pass" : (required ? "fail" : "info");
            var badge = IAPHelperUIStyle.CreateBadge(badgeText, badgeSeverity);
            row.Add(badge);

            return row;
        }

        private List<string> GetAvailableCatalogProductIds()
        {
            var helper = UnityEngine.Object.FindObjectOfType<IAPHelper>();
            if (helper != null && helper.products != null)
            {
                return helper.products
                    .Where(p => !string.IsNullOrEmpty(p.id))
                    .Select(p => p.id)
                    .Distinct()
                    .ToList();
            }

            return new List<string>();
        }
    }
}
