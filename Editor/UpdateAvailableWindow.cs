using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

using UnityEngine;

namespace Wagenheimer.IAPHelper.Editor
{
    internal class UpdateAvailableWindow : EditorWindow
    {
        static readonly Color AccentColor = new Color(0.24f, 0.48f, 0.95f);

        string _packageDisplayName;
        string _currentVersion;
        string _latestVersion;
        string _repoUrl;
        string _gitUrl;
        string _releaseNotes;
        string _skipPrefKey;
        Vector2 _notesScroll;

        AddRequest _addRequest;
        bool _updating;
        string _updateError;

        Texture2D _headerTex;
        Texture2D _dividerTex;
        GUIStyle _headerTitleStyle;
        GUIStyle _headerSubtitleStyle;
        GUIStyle _arrowStyle;
        GUIStyle _primaryButtonStyle;
        GUIStyle _footerStyle;
        bool _stylesBuilt;

        public static void Show(string packageDisplayName, string currentVersion, string latestVersion,
            string repoUrl, string gitUrl, string releaseNotes, string skipPrefKey)
        {
            var window = CreateInstance<UpdateAvailableWindow>();
            window.titleContent = new GUIContent($"{packageDisplayName} — Update");
            window._packageDisplayName = packageDisplayName;
            window._currentVersion = currentVersion;
            window._latestVersion = latestVersion;
            window._repoUrl = repoUrl;
            window._gitUrl = gitUrl;
            window._releaseNotes = string.IsNullOrEmpty(releaseNotes)
                ? "No release notes available — see the full changelog on GitHub."
                : releaseNotes;
            window._skipPrefKey = skipPrefKey;

            var size = new Vector2(460, 420);
            window.minSize = size;
            window.maxSize = new Vector2(640, 760);
            window.ShowUtility();
        }

        void BuildStyles()
        {
            if (_stylesBuilt)
                return;
            _stylesBuilt = true;

            var dark = EditorGUIUtility.isProSkin;
            var headerBg = dark ? new Color(0.17f, 0.17f, 0.19f) : new Color(0.90f, 0.92f, 0.97f);
            var dividerColor = dark ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0.10f);

            _headerTex = MakeTex(headerBg);
            _dividerTex = MakeTex(dividerColor);

            _headerTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                normal = { textColor = dark ? Color.white : new Color(0.12f, 0.12f, 0.14f) }
            };

            _headerSubtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                normal = { textColor = dark ? new Color(1f, 1f, 1f, 0.6f) : new Color(0f, 0f, 0f, 0.55f) }
            };

            _arrowStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = AccentColor }
            };

            _primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };

            _footerStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = dark ? new Color(1f, 1f, 1f, 0.4f) : new Color(0f, 0f, 0f, 0.4f) }
            };
        }

        static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        void OnGUI()
        {
            BuildStyles();

            var headerRect = GUILayoutUtility.GetRect(position.width, 56);
            GUI.DrawTexture(headerRect, _headerTex);
            var innerHeader = new Rect(headerRect.x + 16, headerRect.y + 8, headerRect.width - 32, headerRect.height - 12);
            GUI.BeginGroup(innerHeader);
            GUI.Label(new Rect(0, 0, innerHeader.width, 20), "New version available", _headerTitleStyle);
            GUI.Label(new Rect(0, 20, innerHeader.width, 16), _packageDisplayName, _headerSubtitleStyle);
            GUI.EndGroup();

            DrawDivider();
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label($"Installed: {_currentVersion}", EditorStyles.boldLabel, GUILayout.Width(130));
            GUILayout.Label("→", _arrowStyle, GUILayout.Width(20));
            GUILayout.Label($"Latest: {_latestVersion}", EditorStyles.boldLabel);
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label("What's New", EditorStyles.boldLabel);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            _notesScroll = GUILayout.BeginScrollView(_notesScroll, EditorStyles.helpBox, GUILayout.ExpandHeight(true));
            GUILayout.Label(_releaseNotes, EditorStyles.wordWrappedLabel);
            GUILayout.EndScrollView();
            GUILayout.Space(16);
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_updateError))
            {
                GUILayout.Space(6);
                EditorGUILayout.HelpBox(_updateError, MessageType.Error);
            }

            GUILayout.Space(12);
            DrawDivider();
            GUILayout.Space(10);

            if (_updating)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(16);
                GUILayout.Label("Updating package via Package Manager...", EditorStyles.label);
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(16);

                if (GUILayout.Button($"Update to {_latestVersion}", _primaryButtonStyle, GUILayout.Height(30)))
                    StartUpdate();

                if (GUILayout.Button("View on GitHub", GUILayout.Height(30), GUILayout.Width(110)))
                    Application.OpenURL($"{_repoUrl}/releases");

                GUILayout.Space(16);
                GUILayout.EndHorizontal();

                GUILayout.Space(6);

                if (GUILayout.Button("Skip this version", EditorStyles.miniButton))
                {
                    EditorPrefs.SetString(_skipPrefKey, _latestVersion);
                    Close();
                }
                GUILayout.Space(10);
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label(_repoUrl, _footerStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        void DrawDivider()
        {
            var rect = GUILayoutUtility.GetRect(position.width, 1, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, _dividerTex);
        }

        void StartUpdate()
        {
            _updating = true;
            _updateError = null;
            _addRequest = Client.Add(_gitUrl);
            EditorApplication.update += PollUpdate;
        }

        void PollUpdate()
        {
            if (_addRequest == null || !_addRequest.IsCompleted)
                return;

            EditorApplication.update -= PollUpdate;

            if (this == null)
                return;

            _updating = false;

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[{_packageDisplayName}] Updated to version {_addRequest.Result.version}.");
                Close();
            }
            else
            {
                _updateError = _addRequest.Error?.message ?? "Unknown update failure.";
                Debug.LogError($"[{_packageDisplayName}] Failed to update: {_updateError}");
                Repaint();
            }
        }

        void OnDestroy()
        {
            EditorApplication.update -= PollUpdate;
            if (_headerTex != null) DestroyImmediate(_headerTex);
            if (_dividerTex != null) DestroyImmediate(_dividerTex);
        }
    }
}
