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
        private DocumentShell _shell;
        private string _currentPath;
        private FileSystemWatcher _watcher;

        // Set from the watcher's background thread, drained on the main thread.
        private volatile bool _renderQueued;

        // Last successfully rendered file state. A watcher fires several events per
        // save (and some touches don't change bytes), so this guards against re-reading,
        // re-parsing, and rebuilding the whole tree for unchanged content — the same
        // guard MarkdownInspector already has.
        private string _cachedContent;
        private long _cachedWriteTicks;
        private long _cachedLength;

        // Bounded retry budget for transient save-race read failures; reset per burst.
        private const int MaxIoRetries = 30;
        private volatile int _ioFailures;

        [MenuItem("Window/Kmd/Markdown Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<MarkdownViewerWindow>();
            window.titleContent = new GUIContent("Markdown Viewer");
            window.minSize = new Vector2(300, 400);
        }

        public void ShowFile(string path)
        {
            var full = Path.GetFullPath(path);
            if (full != _currentPath)
            {
                _currentPath = full;
                InvalidateCache();
            }

            titleContent = new GUIContent(Path.GetFileName(_currentPath) + " - Markdown");
            if (!RenderFile(force: false))
            {
                _renderQueued = true; // initial read raced a write — retry via the pump
            }

            SetupWatcher();
        }

        private void InvalidateCache()
        {
            _cachedContent = null;
            _cachedWriteTicks = 0;
            _cachedLength = 0;
            _ioFailures = 0;
        }

        // Returns false ONLY when the read failed transiently (file locked / mid-write),
        // signalling the caller to retry; true means rendered, unchanged, or nothing to
        // do. Pass force=true on the watcher path — a watcher event already proves a
        // write happened, so the cheap stat guard must be bypassed (otherwise a
        // length-preserving edit on a coarse-timestamp volume, e.g. FAT/SMB, would be
        // skipped). The content byte-compare below still elides a redundant re-render.
        private bool RenderFile(bool force)
        {
            if (_renderer == null || string.IsNullOrEmpty(_currentPath) || !File.Exists(_currentPath))
            {
                return true;
            }

            var info = new FileInfo(_currentPath);
            var writeTicks = info.LastWriteTimeUtc.Ticks;
            var length = info.Length;
            if (!force && writeTicks == _cachedWriteTicks && length == _cachedLength)
            {
                return true;
            }

            string content;
            try
            {
                content = File.ReadAllText(_currentPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Mid-write / briefly locked: keep the last render; the caller retries.
                return false;
            }

            if (content == _cachedContent)
            {
                // Bytes identical (a touch, or a length-preserving stamp bump): refresh
                // the stamps and skip the expensive re-parse + tree rebuild.
                _cachedWriteTicks = writeTicks;
                _cachedLength = length;
                return true;
            }

            _cachedContent = content;
            _cachedWriteTicks = writeTicks;
            _cachedLength = length;
            _renderer.BaseDirectory = Path.GetDirectoryName(_currentPath);
            _renderer.Render(content);
            _shell?.Refresh();
            return true;
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
            _shell = new DocumentShell(_renderer);
            rootVisualElement.Add(_shell);
            ThemeManager.Register(rootVisualElement);

            if (!string.IsNullOrEmpty(_currentPath))
            {
                if (!RenderFile(force: false))
                {
                    _renderQueued = true;
                }

                SetupWatcher();
            }
            else
            {
                // Opened (e.g. via the menu) with a .md already selected: show it
                // immediately instead of an empty shell.
                OnSelectionChanged();
            }

            rootVisualElement.RegisterCallback<DragPerformEvent>(OnDragPerform);
            rootVisualElement.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);

            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged += OnSelectionChanged;
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnDisable()
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            Selection.selectionChanged -= OnSelectionChanged;
            ThemeManager.Unregister(rootVisualElement);
            EditorApplication.update -= OnEditorUpdate;
            rootVisualElement.UnregisterCallback<DragPerformEvent>(OnDragPerform);
            rootVisualElement.UnregisterCallback<DragUpdatedEvent>(OnDragUpdated);
            TeardownWatcher();
        }

        // ApplyTheme swaps the stylesheet, but the window's panel doesn't always
        // restyle/repaint on its own — rebuild the document so the new theme applies
        // immediately. Re-render from the in-memory content (not a fresh disk read +
        // re-stat) since only the styling changed; cached image textures are reused.
        private void OnThemeChanged()
        {
            if (!string.IsNullOrEmpty(_cachedContent))
            {
                _renderer.Render(_cachedContent);
                _shell?.Refresh();
            }
            else
            {
                RenderFile(force: false);
            }

            Repaint();
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
            // state off the main thread. Reset the retry budget so a genuine new save
            // gets a fresh set of read attempts.
            _renderQueued = true;
            _ioFailures = 0;
        }

        private void OnEditorUpdate()
        {
            if (!_renderQueued)
            {
                return;
            }

            // Force the read: a watcher event (or a failed initial render) already
            // proves work is pending. If the file is briefly locked, RenderFile returns
            // false — keep retrying on later ticks until it unlocks, bounded so a
            // permanently-locked file doesn't spin forever (a fresh watcher event resets
            // the budget for the next genuine change).
            if (RenderFile(force: true) || ++_ioFailures >= MaxIoRetries)
            {
                _renderQueued = false;
                _ioFailures = 0;
            }
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
