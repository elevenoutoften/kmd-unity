using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    // editorForChildClasses is intentionally left at its default (false): we only
    // want to take over the inspector for plain TextAssets (.md/.txt/.json/...),
    // NOT subclasses such as MonoScript (.cs), whose native importer inspector
    // must be preserved.
    [CustomEditor(typeof(TextAsset))]
    public class MarkdownInspector : Editor
    {
        private const long PollIntervalMs = 300;

        private UIMarkdownRenderer _renderer;
        private VisualElement _root;
        private string _cachedPath;
        private string _cachedContent;
        private long _cachedWriteTicks;
        private long _cachedLength;
        private IVisualElementScheduledItem _pollItem;

        public override VisualElement CreateInspectorGUI()
        {
            if (!IsMarkdownAsset(out _))
            {
                return CreateTextPreview();
            }

            _root = new VisualElement { name = "md-inspector-root" };
            _renderer = new UIMarkdownRenderer();
            _root.Add(_renderer.RootElement);
            ThemeManager.Register(_root);

            TryRender(force: true);
            _pollItem = _root.schedule.Execute(() => TryRender(force: false)).Every(PollIntervalMs);

            return _root;
        }

        // Non-markdown TextAssets (.txt/.json/.csv/.bytes/...) would otherwise show
        // an EMPTY inspector, because overriding TextAsset's editor suppresses
        // Unity's built-in text preview and TextAsset exposes no serialized fields.
        // Render the contents ourselves in a read-only field to preserve the preview.
        private VisualElement CreateTextPreview()
        {
            var asset = target as TextAsset;
            var field = new TextField
            {
                multiline = true,
                isReadOnly = true,
                value = asset != null ? asset.text : string.Empty,
            };
            field.AddToClassList("md-textasset-preview");
            return field;
        }

        private void TryRender(bool force)
        {
            if (_renderer == null || !IsMarkdownAsset(out var path))
            {
                return;
            }

            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return;
            }

            // Cheap change detection first — only pay for a full file read when the
            // file's modified time or size actually changed since the last render.
            var info = new FileInfo(fullPath);
            var writeTicks = info.LastWriteTimeUtc.Ticks;
            var length = info.Length;
            if (!force
                && path == _cachedPath
                && writeTicks == _cachedWriteTicks
                && length == _cachedLength)
            {
                return;
            }

            var content = File.ReadAllText(fullPath);
            if (!force && path == _cachedPath && content == _cachedContent)
            {
                // Timestamp/size moved but the bytes are identical (a touch, or a
                // coarse-resolution volume): refresh the stamps and skip re-render.
                _cachedWriteTicks = writeTicks;
                _cachedLength = length;
                return;
            }

            _cachedPath = path;
            _cachedContent = content;
            _cachedWriteTicks = writeTicks;
            _cachedLength = length;
            _renderer.BaseDirectory = Path.GetDirectoryName(fullPath);
            _renderer.Render(content);
        }

        private void OnDisable()
        {
            ThemeManager.Unregister(_root);
            _pollItem?.Pause();
            _pollItem = null;
            _cachedContent = null;
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
