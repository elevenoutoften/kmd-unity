using Markdig.Renderers;
using Markdig.Syntax.Inlines;

namespace Kmd.MarkdownReader
{
    public class EmphasisInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, EmphasisInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, EmphasisInline obj)
        {
            string open, close;

            // ~~text~~ (EmphasisExtras) is strikethrough; ** / __ is bold; * / _ is italic.
            if (obj.DelimiterChar == '~' && obj.DelimiterCount == 2)
            {
                open = "<s>";
                close = "</s>";
            }
            else if (obj.DelimiterCount >= 2)
            {
                open = "<b>";
                close = "</b>";
            }
            else
            {
                open = "<i>";
                close = "</i>";
            }

            renderer.WriteText(open);
            renderer.WriteChildren(obj);
            renderer.WriteText(close);
        }
    }
}
