using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    /// <summary>
    /// Two-pane shell: a toggleable outline sidebar next to a UIMarkdownRenderer's
    /// scroll container. Outline entries scroll to their heading on click, and the
    /// entry matching the current scroll position is highlighted.
    /// </summary>
    public class DocumentShell : VisualElement
    {
        private const long BasePollIntervalMs = 250;
        private const long MaxPollIntervalMs = 2000;

        private readonly UIMarkdownRenderer _renderer;
        private readonly VisualElement _sidebar;
        private readonly Button _toggle;
        private readonly List<KeyValuePair<OutlineEntry, Button>> _items =
            new List<KeyValuePair<OutlineEntry, Button>>();

        private bool _outlineVisible = true;
        private int _lastActiveIndex = -1;
        private int _unchangedChecks;
        private long _pollIntervalMs = BasePollIntervalMs;
        private IVisualElementScheduledItem _activeHeadingPollItem;

        public DocumentShell(UIMarkdownRenderer renderer)
        {
            _renderer = renderer;
            name = "md-document-shell";
            AddToClassList("md-document-shell");

            // Top bar: outline toggle on the left, theme selector pushed to the right.
            var topbar = new VisualElement { name = "md-topbar" };
            topbar.AddToClassList("md-topbar");
            Add(topbar);

            _toggle = new Button(() => SetOutlineVisible(!_outlineVisible))
            {
                name = "md-outline-toggle",
                text = "☰ Outline",
            };
            _toggle.AddToClassList("md-outline-toggle");
            topbar.Add(_toggle);

            var themeSelect = new DropdownField
            {
                name = "md-theme-select",
                choices = new List<string>(ThemeManager.ThemeChoices),
                value = ThemeManager.CurrentTheme,
                tooltip = "Reader colour theme",
            };
            themeSelect.AddToClassList("md-theme-select");
            themeSelect.RegisterValueChangedCallback(evt => ThemeManager.SetTheme(evt.newValue));
            topbar.Add(themeSelect);

            var row = new VisualElement { name = "md-document-row" };
            row.AddToClassList("md-document-row");
            Add(row);

            _sidebar = new VisualElement { name = "md-outline-sidebar" };
            _sidebar.AddToClassList("md-outline-sidebar");
            row.Add(_sidebar);
            row.Add(_renderer.RootElement);

            // Poll the scroll position to highlight the active heading. Uses only
            // the scheduler (no scroller-event dependency); pauses when detached.
            UpdatePollingState();
        }

        /// <summary>Rebuilds the outline from the renderer's last parsed document.</summary>
        public void Refresh()
        {
            _sidebar.Clear();
            _items.Clear();

            foreach (var entry in OutlineExtractor.Extract(_renderer.Document))
            {
                if (string.IsNullOrEmpty(entry.Text) || string.IsNullOrEmpty(entry.Id))
                {
                    continue;
                }

                var captured = entry;
                var button = new Button(() => _renderer.ScrollToHeading(captured.Id))
                {
                    text = entry.Text,
                };
                button.AddToClassList("md-outline-item");
                button.AddToClassList("md-outline-level-" + entry.Level);
                _sidebar.Add(button);
                _items.Add(new KeyValuePair<OutlineEntry, Button>(entry, button));
            }

            _sidebar.style.display = _items.Count > 0 && _outlineVisible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _toggle.style.display = _items.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _lastActiveIndex = -1;
            _unchangedChecks = 0;
            _pollIntervalMs = BasePollIntervalMs;
            UpdatePollingState();
        }

        private void SetOutlineVisible(bool visible)
        {
            _outlineVisible = visible;
            _sidebar.style.display = visible && _items.Count > 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            if (!visible && _lastActiveIndex >= 0 && _lastActiveIndex < _items.Count)
            {
                _items[_lastActiveIndex].Value.EnableInClassList("md-outline-active", false);
                _lastActiveIndex = -1;
            }

            _unchangedChecks = 0;
            _pollIntervalMs = BasePollIntervalMs;
            UpdatePollingState();
        }

        private void UpdateActiveHeading()
        {
            if (_items.Count == 0 || !_outlineVisible)
            {
                return;
            }

            var top = _renderer.RootElement.worldBound.yMin;
            var activeIndex = -1;
            for (var i = 0; i < _items.Count; i++)
            {
                if (_renderer.TryGetHeading(_items[i].Key.Id, out var element)
                    && element.worldBound.yMin - top <= 8f)
                {
                    activeIndex = i;
                }
            }

            if (activeIndex == _lastActiveIndex)
            {
                _unchangedChecks++;
                if (_unchangedChecks >= 2)
                {
                    SetPollInterval(System.Math.Min(_pollIntervalMs * 2, MaxPollIntervalMs));
                }

                return;
            }

            if (_lastActiveIndex >= 0 && _lastActiveIndex < _items.Count)
            {
                _items[_lastActiveIndex].Value.EnableInClassList("md-outline-active", false);
            }

            if (activeIndex >= 0 && activeIndex < _items.Count)
            {
                _items[activeIndex].Value.EnableInClassList("md-outline-active", true);
            }

            _lastActiveIndex = activeIndex;
            _unchangedChecks = 0;
            SetPollInterval(BasePollIntervalMs);
        }

        private void UpdatePollingState()
        {
            if (!_outlineVisible || _items.Count == 0)
            {
                _activeHeadingPollItem?.Pause();
                return;
            }

            SetPollInterval(_pollIntervalMs, forceRestart: true);
        }

        private void SetPollInterval(long intervalMs, bool forceRestart = false)
        {
            if (!forceRestart && _pollIntervalMs == intervalMs)
            {
                return;
            }

            _pollIntervalMs = intervalMs;
            _activeHeadingPollItem?.Pause();

            if (!_outlineVisible || _items.Count == 0)
            {
                return;
            }

            _activeHeadingPollItem = schedule.Execute(UpdateActiveHeading).Every(_pollIntervalMs);
        }
    }
}
