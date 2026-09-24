using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.IAPHelper.Editor
{
    internal sealed class IAPHelperDocsView
    {
        public VisualElement Root { get; }

        public IAPHelperDocsView()
        {
            Root = new VisualElement();
            IAPHelperUIStyle.Apply(Root);

            BuildUI();
        }

        private void BuildUI()
        {
            // Architecture Card
            var archCard = IAPHelperUIStyle.CreateCard("📚 Unity IAP v5 Architecture", "Best practices and key mechanisms implemented in this package.");
            archCard.Add(IAPHelperUIStyle.CreateCallout(
                "• Two-Step Purchase Flow: Unity IAP v5 requires purchases to enter a 'Pending' state, validate entitlements, and then explicitly call 'ConfirmPendingPurchase'. This package handles this two-step flow automatically with zero risk of transaction loss.\n" +
                "• Auto-Restore Policy: On Android (Google Play & Amazon), purchases are silently restored on game launch. On iOS / Apple App Store, Apple Guideline 3.1.1 prohibits automatic restoration prompts; an explicit 'Restore Purchases' button is mandatory.",
                "info"));
            Root.Add(archCard);

            // Code Examples Card
            var codeCard = IAPHelperUIStyle.CreateCard("💻 Quick Code Examples", "Common runtime interactions in your gameplay scripts.");

            codeCard.Add(new Label("1. Initiating a purchase:") { style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } });
            codeCard.Add(CreateCodeBox(
                "// Trigger purchase with success/failure callbacks:\n" +
                "IAPHelper.Instance.BuyProduct(\"unlockfullgame\",\n" +
                "    onSuccess: (order) => {\n" +
                "        Debug.Log(\"Purchase complete! Unlocking full game...\");\n" +
                "    },\n" +
                "    onFailed: (err, msg) => {\n" +
                "        Debug.LogWarning($\"Purchase failed: {msg}\");\n" +
                "    }\n" +
                ");"));

            codeCard.Add(new Label("2. Checking if a product is owned:") { style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } });
            codeCard.Add(CreateCodeBox(
                "if (IAPHelper.Instance.IsProductOwned(\"unlockfullgame\")) {\n" +
                "    // Product is owned in current session or restored from store receipt\n" +
                "}"));

            codeCard.Add(new Label("3. Manual restore button (iOS / App Store):") { style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } });
            codeCard.Add(CreateCodeBox(
                "IAPHelper.Instance.RestorePurchases((success, msg) => {\n" +
                "    if (success) Debug.Log(\"Purchases restored successfully!\");\n" +
                "});"));

            Root.Add(codeCard);

            // Package Maintenance Card
            var maintCard = IAPHelperUIStyle.CreateCard("🔄 Package Maintenance & Updates");
            var updateBtn = new Button(() => UpdateChecker.CheckForUpdate(force: true)) { text = "Check for Package Updates" };
            updateBtn.AddToClassList("iap-toolbar-btn");
            updateBtn.style.alignSelf = Align.FlexStart;
            maintCard.Add(updateBtn);
            Root.Add(maintCard);
        }

        private VisualElement CreateCodeBox(string code)
        {
            var box = new VisualElement();
            box.AddToClassList("iap-code-box");

            var lbl = new Label(code);
            lbl.AddToClassList("iap-code-text");
            lbl.style.unityFontStyleAndWeight = FontStyle.Normal;
            box.Add(lbl);

            return box;
        }
    }
}
