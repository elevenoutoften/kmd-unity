using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    /// <summary>
    /// Loads and applies the dark/light Markdown stylesheet to render roots based
    /// on the current editor skin, and keeps registered roots in sync when the
    /// editor theme changes mid-session.
    /// </summary>
    public static class ThemeManager
    {
        private const string DarkSheetName = "MarkdownReaderDark";
        private const string LightSheetName = "MarkdownReaderLight";

        private static readonly HashSet<VisualElement> Roots = new HashSet<VisualElement>();
        private static StyleSheet _darkSheet;
        private static StyleSheet _lightSheet;
        private static bool _lastIsDark;
        private static bool _hooked;

        public static bool IsDarkTheme => EditorGUIUtility.isProSkin;

        /// <summary>Path of the stylesheet for the current skin, or null if missing.</summary>
        public static string GetThemeUssPath()
        {
            var sheet = GetSheet(IsDarkTheme);
            return sheet != null ? AssetDatabase.GetAssetPath(sheet) : null;
        }

        /// <summary>
        /// Applies the current theme to <paramref name="root"/> and keeps it updated
        /// when the editor skin changes. Call <see cref="Unregister"/> when the host
        /// (inspector/window) is disabled.
        /// </summary>
        public static void Register(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            Roots.Add(root);
            ApplyTheme(root);

            if (!_hooked)
            {
                _lastIsDark = IsDarkTheme;
                EditorApplication.update += OnEditorUpdate;
                _hooked = true;
            }
        }

        public static void Unregister(VisualElement root)
        {
            if (root != null)
            {
                Roots.Remove(root);
            }
        }

        /// <summary>Swaps in the stylesheet matching the current skin (idempotent).</summary>
        public static void ApplyTheme(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            var dark = GetSheet(true);
            var light = GetSheet(false);

            if (dark != null) root.styleSheets.Remove(dark);
            if (light != null) root.styleSheets.Remove(light);

            var active = IsDarkTheme ? dark : light;
            if (active != null) root.styleSheets.Add(active);
        }

        // UnityEditor exposes no public themeChanged event, so poll the skin on the
        // editor update tick (a cheap bool compare) and re-apply only on a change.
        private static void OnEditorUpdate()
        {
            if (IsDarkTheme == _lastIsDark)
            {
                return;
            }

            _lastIsDark = IsDarkTheme;
            Roots.RemoveWhere(r => r == null || r.panel == null);
            foreach (var root in Roots)
            {
                ApplyTheme(root);
            }
        }

        private static StyleSheet GetSheet(bool dark)
        {
            if (dark)
            {
                if (_darkSheet == null) _darkSheet = LoadSheet(DarkSheetName);
                return _darkSheet;
            }

            if (_lightSheet == null) _lightSheet = LoadSheet(LightSheetName);
            return _lightSheet;
        }

        private static StyleSheet LoadSheet(string sheetName)
        {
            foreach (var guid in AssetDatabase.FindAssets(sheetName + " t:StyleSheet"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/" + sheetName + ".uss", StringComparison.OrdinalIgnoreCase))
                {
                    var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    if (sheet != null)
                    {
                        return sheet;
                    }
                }
            }

            return null;
        }
    }
}
