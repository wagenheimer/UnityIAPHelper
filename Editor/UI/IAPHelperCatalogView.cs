using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.IAPHelper.Editor
{
    internal sealed class IAPHelperCatalogView
    {
        public VisualElement Root { get; }

        public IAPHelperCatalogView()
        {
            Root = new VisualElement();
            IAPHelperUIStyle.Apply(Root);

            BuildUI();
        }

        private void BuildUI()
        {
            var headerCard = IAPHelperUIStyle.CreateCard("📦 In-App Purchase Product Catalog", "Live inspection of products defined in your active IAPHelper component.");

            var helper = UnityEngine.Object.FindObjectOfType<IAPHelper>();
            if (helper == null)
            {
                headerCard.Add(IAPHelperUIStyle.CreateCallout("No IAPHelper component found in the open scene. Add an IAPHelper GameObject to configure products.", "warn"));
                Root.Add(headerCard);
                return;
            }

            var btnRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8 } };
            var selectBtn = new Button(() =>
            {
                Selection.activeGameObject = helper.gameObject;
                EditorGUIUtility.PingObject(helper.gameObject);
            })
            { text = "🎯 Select IAPHelper in Hierarchy" };
            selectBtn.AddToClassList("iap-toolbar-btn");
            btnRow.Add(selectBtn);
            headerCard.Add(btnRow);

            if (helper.products == null || helper.products.Count == 0)
            {
                headerCard.Add(IAPHelperUIStyle.CreateCallout("The products list is currently empty. Configure products in the IAPHelper Inspector.", "info"));
                Root.Add(headerCard);
                return;
            }

            Root.Add(headerCard);

            var listCard = IAPHelperUIStyle.CreateCard($"Configured Products ({helper.products.Count})");

            foreach (var prod in helper.products)
            {
                var card = new VisualElement();
                card.AddToClassList("iap-catalog-card");

                var topRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };
                var idLabel = new Label(prod.id ?? "(empty ID)")
                {
                    style =
                    {
                        fontSize = 13,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        color = Color.white
                    }
                };
                topRow.Add(idLabel);

                var typeBadge = IAPHelperUIStyle.CreateProductTypeBadge(prod.type);
                topRow.Add(typeBadge);
                card.Add(topRow);

                // Store overrides row
                var storesRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 6 } };

                string apple = !string.IsNullOrEmpty(prod.appleId) ? prod.appleId : prod.id;
                storesRow.Add(CreateStoreChip("🍎 Apple", apple));

                string google = !string.IsNullOrEmpty(prod.googlePlayId) ? prod.googlePlayId : prod.id;
                storesRow.Add(CreateStoreChip("🤖 Google", google));

                if (!string.IsNullOrEmpty(prod.amazonId))
                {
                    storesRow.Add(CreateStoreChip("🛒 Amazon", prod.amazonId));
                }

                card.Add(storesRow);

                // Event wiring indicator
                int listeners = (prod.onEntitlementGranted != null ? prod.onEntitlementGranted.GetPersistentEventCount() : 0);
                var eventRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 6 } };
                var eventBadge = IAPHelperUIStyle.CreateBadge(
                    listeners > 0 ? $"✓ {listeners} Entitlement Listener(s)" : "⚠ No Entitlement Listeners Wired",
                    listeners > 0 ? "pass" : "warn");
                eventRow.Add(eventBadge);
                card.Add(eventRow);

                listCard.Add(card);
            }

            Root.Add(listCard);
        }

        private VisualElement CreateStoreChip(string label, string sku)
        {
            var chip = new VisualElement();
            chip.AddToClassList("iap-store-chip");

            var lbl = new Label(label) { style = { fontSize = 10, color = new Color(0.65f, 0.65f, 0.70f), marginRight = 4 } };
            var val = new Label(sku) { style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.85f, 0.85f, 0.90f) } };

            chip.Add(lbl);
            chip.Add(val);
            return chip;
        }
    }
}
