using Markdig.Renderers;
using Markdig.Syntax.Inlines;

namespace Kmd.MarkdownReader
{
    public class LineBreakInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, LineBreakInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, LineBreakInline obj)
        {
            // Hard break -> newline; soft break -> space (so words across wrapped
            // source lines don't run together).
            renderer.WriteText(obj.IsHard ? "\n" : " ");
        }
    }
}
