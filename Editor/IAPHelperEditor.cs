using System;

using UnityEditor;

using UnityEngine;
using UnityEngine.Purchasing;

using Wagenheimer.IAPHelper.UI;

namespace Wagenheimer.IAPHelper.Editor
{
    [CustomEditor(typeof(IAPHelper), true)]
    public class IAPHelperEditor : UnityEditor.Editor
    {
        private SerializedProperty _productsProp;
        private SerializedProperty _initializeOnStartProp;
        private SerializedProperty _autoConfigurePlatformRestoreProp;
        private SerializedProperty _autoRestorePurchasesProp;
        private SerializedProperty _processPendingOnFetchProp;
        private SerializedProperty _logPurchasesFetchFailuresProp;
        private SerializedProperty _enableDebugOverlayProp;

        private bool _showProducts = true;
        private bool _showSettings = true;

        private void OnEnable()
        {
            _productsProp                    = serializedObject.FindProperty("products");
            _initializeOnStartProp           = serializedObject.FindProperty("initializeOnStart");
            _autoConfigurePlatformRestoreProp= serializedObject.FindProperty("autoConfigurePlatformRestore");
            _autoRestorePurchasesProp        = serializedObject.FindProperty("autoRestorePurchases");
            _processPendingOnFetchProp       = serializedObject.FindProperty("processPendingOnFetch");
            _logPurchasesFetchFailuresProp   = serializedObject.FindProperty("logPurchasesFetchFailures");
            _enableDebugOverlayProp          = serializedObject.FindProperty("enableDebugOverlay");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var helper = (IAPHelper)target;

            DrawHeader();
            DrawToolbar(helper);

            EditorGUILayout.Space(6);

            DrawProductsSection();

            EditorGUILayout.Space(6);

            DrawSettingsSection();

            EditorGUILayout.Space(8);

            DrawRuntimeStatus(helper);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.35f, 0.75f, 1f) : new Color(0.1f, 0.35f, 0.75f) }
            };

            EditorGUILayout.LabelField("IAP Helper v1.3.0", titleStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Production-ready monetization framework with multi-product zero-code support.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar(IAPHelper helper)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent(" Dashboard", EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image), GUILayout.Height(26)))
            {
                IAPHelperDashboardWindow.OpenDashboard();
            }

            if (GUILayout.Button(new GUIContent(" Run Audit", EditorGUIUtility.IconContent("d_TestPassed").image), GUILayout.Height(26)))
            {
                IAPHelperDashboardWindow.OpenAuditTab();
            }

            // Overlay status indicator — auto-attach is controlled by enableDebugOverlay field in Settings.
            bool overlayActive = FindObjectOfType<IAPDebugOverlay>() != null;
            using (new EditorGUI.DisabledScope(true))
            {
                string label = overlayActive ? " Debug Overlay: ON" : " Debug Overlay: OFF";
                string icon  = overlayActive ? "d_DebuggerAttached" : "d_DebuggerDisabled";
                GUILayout.Button(new GUIContent(label, EditorGUIUtility.IconContent(icon).image), GUILayout.Height(26));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawProductsSection()
        {
            _showProducts = EditorGUILayout.BeginFoldoutHeaderGroup(_showProducts, $"Products Catalog ({_productsProp.arraySize})");
            if (_showProducts)
            {
                EditorGUILayout.HelpBox("Configure your store products below. You can connect game unlock/reward methods directly into each product's OnEntitlementGranted event with zero code.", MessageType.None);

                for (int i = 0; i < _productsProp.arraySize; i++)
                {
                    SerializedProperty productElem      = _productsProp.GetArrayElementAtIndex(i);
                    SerializedProperty idProp           = productElem.FindPropertyRelative("id");
                    SerializedProperty typeProp         = productElem.FindPropertyRelative("type");
                    SerializedProperty fallbackPriceProp= productElem.FindPropertyRelative("priceFallback");

                    string title     = string.IsNullOrEmpty(idProp.stringValue) ? $"Product {i}" : idProp.stringValue;
                    string typeName  = ((ProductType)typeProp.enumValueIndex).ToString();
                    string priceText = !string.IsNullOrEmpty(fallbackPriceProp.stringValue) ? $" [{fallbackPriceProp.stringValue}]" : "";

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    EditorGUILayout.BeginHorizontal();
                    productElem.isExpanded = EditorGUILayout.Foldout(productElem.isExpanded, $"{title} ({typeName}){priceText}", true, EditorStyles.foldoutHeader);

                    if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        _productsProp.DeleteArrayElementAtIndex(i);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();

                    if (productElem.isExpanded)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(idProp);
                        EditorGUILayout.PropertyField(typeProp);

                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("Store SKU Overrides", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(productElem.FindPropertyRelative("googlePlayId"));
                        EditorGUILayout.PropertyField(productElem.FindPropertyRelative("appleId"));
                        EditorGUILayout.PropertyField(productElem.FindPropertyRelative("amazonId"));

                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("Fallback Metadata (Editor & Offline)", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(productElem.FindPropertyRelative("titleFallback"));
                        EditorGUILayout.PropertyField(productElem.FindPropertyRelative("descriptionFallback"));
                        EditorGUILayout.PropertyField(productElem.FindPropertyRelative("priceFallback"));

                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("Rewards & Persistence", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(productElem.FindPropertyRelative("playerPrefsFallbackKey"));
                        EditorGUILayout.PropertyField(productElem.FindPropertyRelative("onEntitlementGranted"));

                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.EndVertical();
                }

                if (GUILayout.Button("+ Add Product", GUILayout.Height(24)))
                {
                    _productsProp.InsertArrayElementAtIndex(_productsProp.arraySize);
                    var newElem = _productsProp.GetArrayElementAtIndex(_productsProp.arraySize - 1);
                    newElem.FindPropertyRelative("id").stringValue                = "new_product";
                    newElem.FindPropertyRelative("type").enumValueIndex           = (int)ProductType.Consumable;
                    newElem.FindPropertyRelative("titleFallback").stringValue     = "";
                    newElem.FindPropertyRelative("priceFallback").stringValue     = "$0.99";
                    newElem.FindPropertyRelative("playerPrefsFallbackKey").stringValue = "";
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawSettingsSection()
        {
            _showSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showSettings, "Settings & Lifecycle");
            if (_showSettings)
            {
                EditorGUILayout.PropertyField(_initializeOnStartProp);
                EditorGUILayout.PropertyField(_autoConfigurePlatformRestoreProp);

                if (!_autoConfigurePlatformRestoreProp.boolValue)
                {
                    EditorGUILayout.PropertyField(_autoRestorePurchasesProp);
                }
                else
                {
                    EditorGUILayout.HelpBox("Auto-Configure Platform Restore is ENABLED. Current build target autoRestorePurchases will be evaluated at runtime (True on Android/Amazon, False on iOS/macOS).", MessageType.Info);
                }

                EditorGUILayout.PropertyField(_processPendingOnFetchProp);
                EditorGUILayout.PropertyField(_logPurchasesFetchFailuresProp);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Debug & QA", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_enableDebugOverlayProp);
                if (_enableDebugOverlayProp != null && _enableDebugOverlayProp.boolValue)
                {
                    EditorGUILayout.HelpBox("IAPDebugOverlay will be auto-attached at runtime in the Unity Editor and Development Builds. No scene setup or code required.", MessageType.None);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawRuntimeStatus(IAPHelper helper)
        {
            if (!Application.isPlaying)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Live Runtime Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Store Connected: {(helper.IsConnected ? "YES" : "NO")}");
            EditorGUILayout.LabelField($"Products Loaded: {(helper.ProductsLoaded ? "YES" : "NO")}");
            EditorGUILayout.LabelField($"Auto-Restore Setting: {helper.autoRestorePurchases}");
            EditorGUILayout.EndVertical();
        }
    }
}
