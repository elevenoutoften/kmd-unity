using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Extensions.TaskLists;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    public class ListBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, ListBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, ListBlock obj)
        {
            renderer.FlushText();

            var list = new VisualElement { name = "md-list" };
            list.AddToClassList("md-list");
            list.AddToClassList(obj.IsOrdered ? "md-list-ordered" : "md-list-unordered");
            renderer.StartBlock(list);

            var start = 1;
            if (obj.IsOrdered && !int.TryParse(obj.OrderedStart, out start))
            {
                start = 1;
            }

            var index = 0;
            foreach (var child in obj)
            {
                if (!(child is ListItemBlock item))
                {
                    continue;
                }

                string marker;
                if (IsTaskItem(item))
                {
                    // The checkbox glyph (TaskListInlineRenderer) is the marker.
                    marker = string.Empty;
                }
                else if (obj.IsOrdered)
                {
                    marker = (start + index) + obj.OrderedDelimiter.ToString();
                }
                else
                {
                    marker = "•"; // bullet
                }

                WriteItem(renderer, item, marker);
                index++;
            }

            renderer.FinishBlock();
        }

        private static void WriteItem(UIMarkdownRenderer renderer, ListItemBlock item, string marker)
        {
            renderer.FlushText();

            var row = new VisualElement { name = "md-list-item" };
            row.AddToClassList("md-list-item");
            renderer.StartBlock(row);

            var markerLabel = new Label(marker) { name = "md-list-marker" };
            markerLabel.AddToClassList("md-list-marker");
            renderer.AddToCurrentBlock(markerLabel);

            var content = new VisualElement { name = "md-list-item-content" };
            content.AddToClassList("md-list-item-content");
            renderer.StartBlock(content);
            renderer.WriteChildren(item);
            renderer.FinishBlock(); // content

            renderer.FinishBlock(); // row
        }

        private static bool IsTaskItem(ListItemBlock item)
        {
            if (item.Count > 0
                && item[0] is ParagraphBlock paragraph
                && paragraph.Inline != null)
            {
                return paragraph.Inline.FirstChild is TaskList;
            }

            return false;
        }
    }
}
