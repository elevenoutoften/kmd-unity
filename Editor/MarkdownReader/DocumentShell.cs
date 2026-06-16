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
        private const float ActiveHeadingTopInset = 16f;
        private const float ScrollEpsilon = 0.5f;

        private readonly UIMarkdownRenderer _renderer;
        private readonly ScrollView _sidebar;
        private readonly Button _toggle;
        private readonly List<KeyValuePair<OutlineEntry, Button>> _items =
            new List<KeyValuePair<OutlineEntry, Button>>();

        private bool _outlineVisible = true;
        private int _lastActiveIndex = -1;
        private bool _activeUpdateScheduled;
        private float _lastScrollOffsetY = float.NaN;

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

            _sidebar = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "md-outline-sidebar",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
            };
            _sidebar.AddToClassList("md-outline-sidebar");
            _sidebar.contentContainer.AddToClassList("md-outline-list");
            row.Add(_sidebar);
            row.Add(_renderer.RootElement);

            _renderer.RootElement.verticalScroller.valueChanged += OnDocumentScrollChanged;
            RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
        }

        /// <summary>Rebuilds the outline from the renderer's last parsed document.</summary>
        public void Refresh()
        {
            _sidebar.contentContainer.Clear();
            _items.Clear();

            foreach (var entry in OutlineExtractor.Extract(_renderer.Document))
            {
                if (string.IsNullOrEmpty(entry.Text) || string.IsNullOrEmpty(entry.Anchor))
                {
                    continue;
                }

                var captured = entry;
                var button = new Button(() => _renderer.ScrollToOutlineHeading(captured.Anchor))
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
            _lastScrollOffsetY = float.NaN;
            ScheduleActiveHeadingUpdate();
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

            if (visible)
            {
                _lastScrollOffsetY = float.NaN;
                ScheduleActiveHeadingUpdate();
            }
        }

        private void UpdateActiveHeading()
        {
            if (_items.Count == 0 || !_outlineVisible)
            {
                return;
            }

            var top = _renderer.RootElement.worldBound.yMin + ActiveHeadingTopInset;
            var activeIndex = -1;
            for (var i = 0; i < _items.Count; i++)
            {
                if (_renderer.TryGetOutlineHeading(_items[i].Key.Anchor, out var element)
                    && element.worldBound.yMin <= top)
                {
                    activeIndex = i;
                }
            }

            if (activeIndex == _lastActiveIndex)
            {
                return;
            }

            if (_lastActiveIndex >= 0 && _lastActiveIndex < _items.Count)
            {
                _items[_lastActiveIndex].Value.EnableInClassList("md-outline-active", false);
            }

            if (activeIndex >= 0 && activeIndex < _items.Count)
            {
                _items[activeIndex].Value.EnableInClassList("md-outline-active", true);
                _sidebar.ScrollTo(_items[activeIndex].Value);
            }

            _lastActiveIndex = activeIndex;
        }

        private void OnDocumentScrollChanged(float offsetY)
        {
            if (!_outlineVisible || _items.Count == 0)
            {
                return;
            }

            if (!float.IsNaN(_lastScrollOffsetY)
                && System.Math.Abs(offsetY - _lastScrollOffsetY) < ScrollEpsilon)
            {
                return;
            }

            _lastScrollOffsetY = offsetY;
            ScheduleActiveHeadingUpdate();
        }

        private void ScheduleActiveHeadingUpdate()
        {
            if (_activeUpdateScheduled || !_outlineVisible || _items.Count == 0)
            {
                return;
            }

            _activeUpdateScheduled = true;
            schedule.Execute(() =>
            {
                _activeUpdateScheduled = false;
                UpdateActiveHeading();
            });
        }

        private void OnDetachedFromPanel(DetachFromPanelEvent evt)
        {
            _renderer.RootElement.verticalScroller.valueChanged -= OnDocumentScrollChanged;
        }
    }
}
