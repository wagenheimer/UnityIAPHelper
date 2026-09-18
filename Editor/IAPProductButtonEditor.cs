using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;

using Wagenheimer.IAPHelper.UI;

namespace Wagenheimer.IAPHelper.Editor
{
    [CustomEditor(typeof(IAPProductButton), true)]
    public class IAPProductButtonEditor : UnityEditor.Editor
    {
        private SerializedProperty _productIdProp;
        private SerializedProperty _buttonBuyProp;
        private SerializedProperty _labelPriceProp;
        private SerializedProperty _labelTitleProp;
        private SerializedProperty _labelDescriptionProp;
        private SerializedProperty _imageIconProp;
        private SerializedProperty _ownedBadgeProp;
        private SerializedProperty _loadingIndicatorProp;
        private SerializedProperty _ownedStateBehaviorProp;
        private SerializedProperty _ownedTextProp;
        private SerializedProperty _onPurchaseSuccessProp;
        private SerializedProperty _onPurchaseFailedProp;

        private void OnEnable()
        {
            _productIdProp = serializedObject.FindProperty("productId");
            _buttonBuyProp = serializedObject.FindProperty("buttonBuy");
            _labelPriceProp = serializedObject.FindProperty("labelPrice");
            _labelTitleProp = serializedObject.FindProperty("labelTitle");
            _labelDescriptionProp = serializedObject.FindProperty("labelDescription");
            _imageIconProp = serializedObject.FindProperty("imageIcon");
            _ownedBadgeProp = serializedObject.FindProperty("ownedBadge");
            _loadingIndicatorProp = serializedObject.FindProperty("loadingIndicator");
            _ownedStateBehaviorProp = serializedObject.FindProperty("ownedStateBehavior");
            _ownedTextProp = serializedObject.FindProperty("ownedText");
            _onPurchaseSuccessProp = serializedObject.FindProperty("onPurchaseSuccess");
            _onPurchaseFailedProp = serializedObject.FindProperty("onPurchaseFailed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var comp = (IAPProductButton)target;

            DrawProductSelector(comp);

            EditorGUILayout.Space(4);

            DrawBindingStatus(comp);

            EditorGUILayout.Space(4);

            DrawUIBindings();

            EditorGUILayout.Space(4);

            DrawOwnedBehavior();

            EditorGUILayout.Space(4);

            DrawEvents();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProductSelector(IAPProductButton comp)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Product Selection", EditorStyles.boldLabel);

            var availableIds = GetAvailableCatalogProductIds();
            if (availableIds.Count > 0)
            {
                int currentIndex = availableIds.IndexOf(_productIdProp.stringValue);
                var options = new List<string>(availableIds);
                options.Add("[Custom ID...]");

                int selected = currentIndex >= 0 ? currentIndex : options.Count - 1;
                int newSelected = EditorGUILayout.Popup("Catalog Product", selected, options.ToArray());

                if (newSelected >= 0 && newSelected < availableIds.Count)
                {
                    _productIdProp.stringValue = availableIds[newSelected];
                }
            }

            EditorGUILayout.PropertyField(_productIdProp, new GUIContent("Product ID"));
            EditorGUILayout.EndVertical();
        }

        private void DrawBindingStatus(IAPProductButton comp)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("UI Component Bindings", EditorStyles.boldLabel);

            if (GUILayout.Button("Auto-Resolve", GUILayout.Width(100), GUILayout.Height(18)))
            {
                comp.AutoResolveComponents();
                EditorUtility.SetDirty(comp);
            }
            EditorGUILayout.EndHorizontal();

            DrawStatusItem("Buy Button", comp.buttonBuy != null, required: true);
            DrawStatusItem("Price Label (TMP)", comp.labelPrice != null, required: true);
            DrawStatusItem("Title Label (TMP)", comp.labelTitle != null, required: false);
            DrawStatusItem("Loading Indicator", comp.loadingIndicator != null, required: false);
            DrawStatusItem("Owned Badge", comp.ownedBadge != null, required: false);

            EditorGUILayout.EndVertical();
        }

        private void DrawStatusItem(string label, bool hasReference, bool required)
        {
            EditorGUILayout.BeginHorizontal();
            string icon = hasReference ? "✓" : (required ? "✕" : "–");
            string color = hasReference ? "green" : (required ? "red" : "gray");
            EditorGUILayout.LabelField($"<color={color}><b>{icon}</b></color> {label}", new GUIStyle(EditorStyles.label) { richText = true });
            EditorGUILayout.EndHorizontal();
        }

        private void DrawUIBindings()
        {
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_buttonBuyProp);
            EditorGUILayout.PropertyField(_labelPriceProp);
            EditorGUILayout.PropertyField(_labelTitleProp);
            EditorGUILayout.PropertyField(_labelDescriptionProp);
            EditorGUILayout.PropertyField(_imageIconProp);
            EditorGUILayout.PropertyField(_ownedBadgeProp);
            EditorGUILayout.PropertyField(_loadingIndicatorProp);
        }

        private void DrawOwnedBehavior()
        {
            EditorGUILayout.LabelField("Owned State Behavior", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_ownedStateBehaviorProp);
            EditorGUILayout.PropertyField(_ownedTextProp);
        }

        private void DrawEvents()
        {
            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_onPurchaseSuccessProp);
            EditorGUILayout.PropertyField(_onPurchaseFailedProp);
        }

        private List<string> GetAvailableCatalogProductIds()
        {
            var helper = FindObjectOfType<IAPHelper>();
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
