using Markdig.Renderers;
using Markdig.Syntax.Inlines;

namespace Kmd.MarkdownReader
{
    public class EmphasisInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, EmphasisInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, EmphasisInline obj)
        {
            if (obj.DelimiterCount >= 2)
            {
                renderer.WriteText("<b>");
                renderer.WriteChildren(obj);
                renderer.WriteText("</b>");
            }
            else
            {
                renderer.WriteText("<i>");
                renderer.WriteChildren(obj);
                renderer.WriteText("</i>");
            }
        }
    }
}
