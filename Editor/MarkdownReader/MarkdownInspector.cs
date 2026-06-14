using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    [CustomEditor(typeof(TextAsset), true)]
    public class MarkdownInspector : Editor
    {
        private const double DebounceSeconds = 0.3;

        private UIMarkdownRenderer _renderer;
        private VisualElement _root;
        private string _cachedPath;
        private string _cachedContent;
        private double _lastRenderTime;

        public override VisualElement CreateInspectorGUI()
        {
            if (!IsMarkdownAsset(out _))
            {
                return new IMGUIContainer(() => DrawDefaultInspector());
            }

            _root = new VisualElement { name = "md-inspector-root" };
            _renderer = new UIMarkdownRenderer();
            _root.Add(_renderer.RootElement);

            TryRender(force: true);
            _root.schedule.Execute(() => TryRender(force: false)).Every((long)(DebounceSeconds * 1000));

            return _root;
        }

        public override void OnInspectorGUI()
        {
            if (!IsMarkdownAsset(out _))
            {
                DrawDefaultInspector();
                return;
            }

            TryRender(force: false);
        }

        private void OnEnable()
        {
            TryRender(force: true);
        }

        private void TryRender(bool force)
        {
            if (_renderer == null || !IsMarkdownAsset(out var path))
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (!force && now - _lastRenderTime < DebounceSeconds)
            {
                return;
            }

            _lastRenderTime = now;

            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return;
            }

            var content = File.ReadAllText(fullPath);
            if (!force && path == _cachedPath && content == _cachedContent)
            {
                return;
            }

            _cachedPath = path;
            _cachedContent = content;
            _renderer.Render(content);
        }

        private bool IsMarkdownAsset(out string path)
        {
            path = null;

            var asset = target as TextAsset;
            if (asset == null)
            {
                return false;
            }

            path = AssetDatabase.GetAssetPath(asset);
            return !string.IsNullOrEmpty(path)
                && path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        }
    }
}
