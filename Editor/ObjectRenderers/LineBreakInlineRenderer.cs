using Markdig.Renderers;
using Markdig.Syntax.Inlines;

namespace Kmd.MarkdownReader
{
    public class LineBreakInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, LineBreakInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, LineBreakInline obj)
        {
            if (obj.IsHard)
            {
                renderer.WriteText("\n");
            }
        }
    }
}
