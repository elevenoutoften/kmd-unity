using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    /// <summary>
    /// Loads and applies the Markdown stylesheet for the selected theme to render
    /// roots, and keeps registered roots in sync when the editor skin or the theme
    /// changes mid-session.
    ///
    /// Two themes are offered: "kmd" (the branded palette, with separate dark/light
    /// sheets chosen by the editor skin) and "Unity" (a single sheet driven by the
    /// editor's own --unity-colors-* variables, so it blends into the Editor and
    /// follows the skin automatically). The choice persists in EditorPrefs.
    /// </summary>
    public static class ThemeManager
    {
        private const string DarkSheetName = "MarkdownReaderDark";
        private const string LightSheetName = "MarkdownReaderLight";
        private const string UnitySheetName = "MarkdownReaderUnity";
        private const string ThemePrefKey = "Kmd.MarkdownReader.Theme";

        public const string ThemeKmd = "kmd";
        public const string ThemeUnity = "Unity";

        /// <summary>Theme names in display order, for the theme selector.</summary>
        public static readonly string[] ThemeChoices = { ThemeKmd, ThemeUnity };

        /// <summary>
        /// Raised after the theme changes. Swapping the stylesheet restyles a custom
        /// inspector element live, but an EditorWindow's panel doesn't always repaint
        /// on its own, so hosts subscribe to this to re-render/repaint themselves.
        /// </summary>
        public static event Action ThemeChanged;

        private static readonly HashSet<VisualElement> Roots = new HashSet<VisualElement>();
        private static StyleSheet _darkSheet;
        private static StyleSheet _lightSheet;
        private static StyleSheet _unitySheet;
        private static bool _lastIsDark;
        private static bool _hooked;
        private static bool _needsApply;

        public static bool IsDarkTheme => EditorGUIUtility.isProSkin;

        /// <summary>The selected theme ("kmd" or "Unity"); persisted across sessions.</summary>
        public static string CurrentTheme
        {
            get => EditorPrefs.GetString(ThemePrefKey, ThemeKmd) == ThemeUnity ? ThemeUnity : ThemeKmd;
        }

        /// <summary>Switches theme and re-applies it to every registered root live.</summary>
        public static void SetTheme(string theme)
        {
            var normalized = theme == ThemeUnity ? ThemeUnity : ThemeKmd;
            if (normalized == CurrentTheme)
            {
                return;
            }

            EditorPrefs.SetString(ThemePrefKey, normalized);
            Roots.RemoveWhere(r => r == null || r.panel == null);
            foreach (var root in Roots)
            {
                ApplyTheme(root);
            }

            ThemeChanged?.Invoke();
        }

        /// <summary>Path of the active stylesheet, or null if it isn't importable yet.</summary>
        public static string GetThemeUssPath()
        {
            var sheet = GetActiveSheet();
            return sheet != null ? AssetDatabase.GetAssetPath(sheet) : null;
        }

        /// <summary>
        /// Applies the current theme to <paramref name="root"/> and keeps it updated
        /// when the editor skin or theme changes. Call <see cref="Unregister"/> when
        /// the host (inspector/window) is disabled.
        /// </summary>
        public static void Register(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            Roots.Add(root);
            ApplyTheme(root);
            _needsApply = true;

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

        /// <summary>Swaps in the stylesheet matching the current theme + skin (idempotent).</summary>
        public static void ApplyTheme(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            var dark = GetSheet(SheetKind.Dark);
            var light = GetSheet(SheetKind.Light);
            var unity = GetSheet(SheetKind.Unity);

            if (dark != null) root.styleSheets.Remove(dark);
            if (light != null) root.styleSheets.Remove(light);
            if (unity != null) root.styleSheets.Remove(unity);

            var active = GetActiveSheet();
            if (active != null) root.styleSheets.Add(active);
        }

        // UnityEditor exposes no public themeChanged event, so poll the skin on the
        // editor update tick (a cheap bool compare) and re-apply only on a change.
        // (The "kmd" theme has separate dark/light sheets; "Unity" follows the skin
        // through its variables, so a skin flip there is a no-op re-apply.)
        private static void OnEditorUpdate()
        {
            var dark = IsDarkTheme;
            if (dark == _lastIsDark && !_needsApply)
            {
                return;
            }

            _lastIsDark = dark;
            Roots.RemoveWhere(r => r == null || r.panel == null);
            foreach (var root in Roots)
            {
                ApplyTheme(root);
            }

            // If the stylesheet wasn't importable yet (e.g. right after a domain
            // reload), keep retrying on later ticks until it loads and applies.
            _needsApply = Roots.Count > 0 && GetActiveSheet() == null;
        }

        private static StyleSheet GetActiveSheet()
        {
            if (CurrentTheme == ThemeUnity)
            {
                return GetSheet(SheetKind.Unity);
            }

            return GetSheet(IsDarkTheme ? SheetKind.Dark : SheetKind.Light);
        }

        private enum SheetKind { Dark, Light, Unity }

        private static StyleSheet GetSheet(SheetKind kind)
        {
            switch (kind)
            {
                case SheetKind.Unity:
                    if (_unitySheet == null) _unitySheet = LoadSheet(UnitySheetName);
                    return _unitySheet;
                case SheetKind.Light:
                    if (_lightSheet == null) _lightSheet = LoadSheet(LightSheetName);
                    return _lightSheet;
                default:
                    if (_darkSheet == null) _darkSheet = LoadSheet(DarkSheetName);
                    return _darkSheet;
            }
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
