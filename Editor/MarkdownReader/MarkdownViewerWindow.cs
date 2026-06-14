using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    public class MarkdownViewerWindow : EditorWindow
    {
        private UIMarkdownRenderer _renderer;
        private string _currentPath;
        private FileSystemWatcher _watcher;

        // Set from the watcher's background thread, drained on the main thread.
        private volatile bool _renderQueued;

        [MenuItem("Window/Kmd/Markdown Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<MarkdownViewerWindow>();
            window.titleContent = new GUIContent("Markdown Viewer");
            window.minSize = new Vector2(300, 400);
        }

        public void ShowFile(string path)
        {
            _currentPath = Path.GetFullPath(path);
            titleContent = new GUIContent(Path.GetFileName(_currentPath) + " - Markdown");
            RenderFile();
            SetupWatcher();
        }

        private void RenderFile()
        {
            if (string.IsNullOrEmpty(_currentPath) || !File.Exists(_currentPath))
            {
                return;
            }

            _renderer?.RenderFile(_currentPath);
        }

        private void SetupWatcher()
        {
            TeardownWatcher();

            if (string.IsNullOrEmpty(_currentPath))
            {
                return;
            }

            var dir = Path.GetDirectoryName(_currentPath);
            var filename = Path.GetFileName(_currentPath);

            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return;
            }

            _watcher = new FileSystemWatcher(dir)
            {
                Filter = filename,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };

            _watcher.Changed += OnWatchedFileChanged;
        }

        private void TeardownWatcher()
        {
            if (_watcher == null)
            {
                return;
            }

            _watcher.Changed -= OnWatchedFileChanged;
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        private void OnEnable()
        {
            rootVisualElement.Clear();

            _renderer = new UIMarkdownRenderer();
            rootVisualElement.Add(_renderer.RootElement);
            ThemeManager.Register(rootVisualElement);

            if (!string.IsNullOrEmpty(_currentPath))
            {
                RenderFile();
                SetupWatcher();
            }

            rootVisualElement.RegisterCallback<DragPerformEvent>(OnDragPerform);
            rootVisualElement.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);

            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            ThemeManager.Unregister(rootVisualElement);
            EditorApplication.update -= OnEditorUpdate;
            rootVisualElement.UnregisterCallback<DragPerformEvent>(OnDragPerform);
            rootVisualElement.UnregisterCallback<DragUpdatedEvent>(OnDragUpdated);
            TeardownWatcher();
        }

        private void OnSelectionChanged()
        {
            var asset = Selection.activeObject as TextAsset;
            if (asset == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                ShowFile(path);
            }
        }

        private void OnWatchedFileChanged(object sender, FileSystemEventArgs args)
        {
            // FileSystemWatcher raises this on a background thread and emits several
            // events per save. Just flag it; the main-thread pump below coalesces
            // the burst into a single render and avoids touching UIToolkit/editor
            // state off the main thread.
            _renderQueued = true;
        }

        private void OnEditorUpdate()
        {
            if (!_renderQueued)
            {
                return;
            }

            _renderQueued = false;
            RenderFile();
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (HasSingleMarkdownPath())
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }
            else
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            }
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            var paths = DragAndDrop.paths;
            if (HasSingleMarkdownPath())
            {
                DragAndDrop.AcceptDrag();
                ShowFile(paths[0]);
            }
        }

        private static bool HasSingleMarkdownPath()
        {
            var paths = DragAndDrop.paths;
            return paths != null
                && paths.Length == 1
                && paths[0].EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        }
    }
}
