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
        private readonly UIMarkdownRenderer _renderer;
        private readonly VisualElement _sidebar;
        private readonly Button _toggle;
        private readonly List<KeyValuePair<OutlineEntry, Button>> _items =
            new List<KeyValuePair<OutlineEntry, Button>>();

        private bool _outlineVisible = true;

        public DocumentShell(UIMarkdownRenderer renderer)
        {
            _renderer = renderer;
            name = "md-document-shell";
            AddToClassList("md-document-shell");

            _toggle = new Button(() => SetOutlineVisible(!_outlineVisible))
            {
                name = "md-outline-toggle",
                text = "☰ Outline",
            };
            _toggle.AddToClassList("md-outline-toggle");
            Add(_toggle);

            var row = new VisualElement { name = "md-document-row" };
            row.AddToClassList("md-document-row");
            Add(row);

            _sidebar = new VisualElement { name = "md-outline-sidebar" };
            _sidebar.AddToClassList("md-outline-sidebar");
            row.Add(_sidebar);
            row.Add(_renderer.RootElement);

            // Poll the scroll position to highlight the active heading. Uses only
            // the scheduler (no scroller-event dependency); pauses when detached.
            schedule.Execute(UpdateActiveHeading).Every(250);
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
        }

        private void SetOutlineVisible(bool visible)
        {
            _outlineVisible = visible;
            _sidebar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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

            for (var i = 0; i < _items.Count; i++)
            {
                _items[i].Value.EnableInClassList("md-outline-active", i == activeIndex);
            }
        }
    }
}
