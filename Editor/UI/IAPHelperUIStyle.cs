using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.IAPHelper.Editor
{
    internal static class IAPHelperUIStyle
    {
        private const string PackageUssPath = "Packages/com.wagenheimer.iaphelper/Editor/UI/IAPHelperCommon.uss";
        private const string LocalUssPath = "Assets/Editor/UI/IAPHelperCommon.uss";

        public static void Apply(VisualElement element)
        {
            if (element == null) return;

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(PackageUssPath);
            if (sheet == null)
            {
                sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(LocalUssPath);
            }

            if (sheet != null)
            {
                element.styleSheets.Add(sheet);
            }
        }

        public static VisualElement CreateCard(string title, string subtitle = null)
        {
            var card = new VisualElement();
            card.AddToClassList("iap-card");

            var header = new VisualElement();
            header.AddToClassList("iap-card-header");

            var titleCol = new VisualElement();
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("iap-card-title");
            titleCol.Add(titleLabel);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var subLabel = new Label(subtitle);
                subLabel.AddToClassList("iap-card-subtitle");
                titleCol.Add(subLabel);
            }

            header.Add(titleCol);
            card.Add(header);

            return card;
        }

        public static Label CreateBadge(string text, string severity)
        {
            var badge = new Label(text);
            badge.AddToClassList("iap-badge");

            switch (severity?.ToLowerInvariant())
            {
                case "pass":
                case "ok":
                    badge.AddToClassList("iap-badge-pass");
                    break;
                case "warn":
                case "warning":
                    badge.AddToClassList("iap-badge-warn");
                    break;
                case "fail":
                case "error":
                    badge.AddToClassList("iap-badge-fail");
                    break;
                default:
                    badge.AddToClassList("iap-badge-info");
                    break;
            }

            return badge;
        }

        public static Label CreateProductTypeBadge(UnityEngine.Purchasing.ProductType type)
        {
            var badge = new Label(type.ToString());
            badge.AddToClassList("iap-badge");

            switch (type)
            {
                case UnityEngine.Purchasing.ProductType.Consumable:
                    badge.AddToClassList("iap-type-consumable");
                    break;
                case UnityEngine.Purchasing.ProductType.NonConsumable:
                    badge.AddToClassList("iap-type-nonconsumable");
                    break;
                case UnityEngine.Purchasing.ProductType.Subscription:
                    badge.AddToClassList("iap-type-subscription");
                    break;
            }

            return badge;
        }

        public static VisualElement CreateCallout(string message, string level = "info")
        {
            var box = new VisualElement();
            box.AddToClassList("iap-callout");

            switch (level?.ToLowerInvariant())
            {
                case "pass":
                case "ok":
                    box.AddToClassList("iap-callout-pass");
                    break;
                case "warn":
                case "warning":
                    box.AddToClassList("iap-callout-warn");
                    break;
                default:
                    box.AddToClassList("iap-callout-info");
                    break;
            }

            var lbl = new Label(message) { style = { whiteSpace = WhiteSpace.Normal } };
            box.Add(lbl);
            return box;
        }
    }
}
