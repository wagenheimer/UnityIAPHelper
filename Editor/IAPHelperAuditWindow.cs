using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.IAPHelper.Editor
{
    internal class IAPHelperAuditWindow : EditorWindow
    {
        private List<AuditResult> _results = new List<AuditResult>();

        public static void ShowWindow(List<AuditResult> results)
        {
            IAPHelperDashboardWindow.OpenAuditTab();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexGrow = 1;
            IAPHelperUIStyle.Apply(root);

            var card = IAPHelperUIStyle.CreateCard("🔍 IAP Setup Verification Audit", "Automated scan of scene components, package settings, and product wiring.");
            var openDashBtn = new Button(IAPHelperDashboardWindow.OpenAuditTab) { text = "Open In IAP Helper Dashboard" };
            openDashBtn.AddToClassList("iap-toolbar-btn");
            card.Add(openDashBtn);
            root.Add(card);

            root.Add(new IAPHelperAuditView().Root);
        }
    }
}
