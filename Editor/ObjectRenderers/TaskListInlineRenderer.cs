using Markdig.Renderers;
using Markdig.Extensions.TaskLists;

namespace Kmd.MarkdownReader
{
    // Renders the [ ] / [x] marker of a GFM task-list item as a read-only
    // checkbox glyph inline at the start of the item's text.
    public class TaskListInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, TaskList>
    {
        protected override void Write(UIMarkdownRenderer renderer, TaskList obj)
        {
            renderer.WriteText(obj.Checked ? "☑ " : "☐ "); // checked / unchecked box
        }
    }
}
