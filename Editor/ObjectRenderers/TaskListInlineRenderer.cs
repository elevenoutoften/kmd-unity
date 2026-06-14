using Markdig.Renderers;
using Markdig.Extensions.TaskLists;
using UnityEditor;

namespace Kmd.MarkdownReader
{
    // Renders the [ ] / [x] marker of a GFM task-list item as a read-only
    // checkbox glyph inline at the start of the item's text.
    public class TaskListInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, TaskList>
    {
        protected override void Write(UIMarkdownRenderer renderer, TaskList obj)
        {
            if (obj.Checked)
            {
                var color = EditorGUIUtility.isProSkin ? "#9b6dff" : "#7c4dff";
                renderer.WriteText("<color=" + color + ">☑</color> "); // filled box
            }
            else
            {
                renderer.WriteText("☐ "); // empty box
            }
        }
    }
}
