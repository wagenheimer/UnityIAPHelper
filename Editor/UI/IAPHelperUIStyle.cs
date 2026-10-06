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
            var titleLabel = CreateIconLabel(title);
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

        /// <summary>
        /// Renders <paramref name="text"/> on the button, splitting a leading icon (emoji/symbol) into its own
        /// element with a reserved width. Inline, a fallback emoji glyph draws wider than it measures, so the
        /// following text runs over it ("◻heck Updates"); a separate, min-width'd element keeps them apart.
        /// </summary>
        public static void ApplyIconText(Button button, string text)
        {
            // Idempotent: drop icon/text children from a previous call so live updates can re-apply cleanly.
            for (int i = button.childCount - 1; i >= 0; i--)
            {
                var child = button[i];
                if (child.ClassListContains("iap-btn-icon") || child.ClassListContains("iap-btn-text"))
                    child.RemoveFromHierarchy();
            }

            SplitLeadingIcon(text, out var icon, out var label);

            if (string.IsNullOrEmpty(icon))
            {
                button.text = text;
                return;
            }

            button.text = string.Empty;
            var iconElement = CreateIconElement(icon);
            if (string.IsNullOrEmpty(label)) iconElement.style.marginRight = 0;
            button.Add(iconElement);

            if (!string.IsNullOrEmpty(label))
            {
                var textLabel = new Label(label);
                textLabel.AddToClassList("iap-btn-text");
                textLabel.pickingMode = PickingMode.Ignore;
                button.Add(textLabel);
            }
        }

        private static Label CreateIconElement(string icon)
        {
            var iconLabel = new Label(icon);
            iconLabel.AddToClassList("iap-btn-icon");
            iconLabel.pickingMode = PickingMode.Ignore;
            return iconLabel;
        }

        /// <summary>
        /// A label whose leading icon is a separate element (same overlap fix as buttons). The returned
        /// element is styled as usual: text color/font properties inherit down to the child label.
        /// </summary>
        public static VisualElement CreateIconLabel(string text)
        {
            SplitLeadingIcon(text, out var icon, out var rest);

            if (string.IsNullOrEmpty(icon))
                return new Label(text);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.Add(CreateIconElement(icon));

            var label = new Label(rest);
            label.pickingMode = PickingMode.Ignore;
            row.Add(label);
            return row;
        }

        /// <summary>
        /// Splits a leading run of icon code points from the rest of a label. Deliberately conservative: only
        /// arrows/symbols and pictographic emoji count, so ordinary words (including accented ones) stay intact.
        /// </summary>
        internal static void SplitLeadingIcon(string text, out string icon, out string label)
        {
            icon = null;
            label = text;
            if (string.IsNullOrEmpty(text)) return;

            int i = 0;
            while (i < text.Length)
            {
                int codePoint = char.IsHighSurrogate(text[i]) && i + 1 < text.Length
                    ? char.ConvertToUtf32(text[i], text[i + 1])
                    : text[i];

                if (!IsIconCodePoint(codePoint)) break;
                i += char.IsHighSurrogate(text[i]) ? 2 : 1;
            }

            if (i == 0) return;

            icon = text.Substring(0, i).TrimEnd();
            label = text.Substring(i).TrimStart();
        }

        private static bool IsIconCodePoint(int codePoint) =>
            (codePoint >= 0x2190 && codePoint <= 0x2BFF)     // arrows, geometric shapes, misc symbols (↗ ▶ ↺ ⚡ ✔ ⚙ ✓ ✕ …)
            || (codePoint >= 0x1F000 && codePoint <= 0x1FAFF) // emoji & pictographs (🔄 🌐 📦 🔍 🎯 ✨ …)
            || codePoint == 0xFE0F                             // emoji variation selector (✉️ 🛠️ …)
            || codePoint == 0x20E3;                            // combining enclosing keycap
    }
}
