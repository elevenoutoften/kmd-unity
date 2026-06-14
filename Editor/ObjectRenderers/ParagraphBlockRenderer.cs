using Markdig.Renderers;
using Markdig.Syntax;

namespace Kmd.MarkdownReader
{
    public class ParagraphBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, ParagraphBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, ParagraphBlock obj)
        {
            renderer.StartNewText();
            renderer.WriteChildren(obj.Inline);
            renderer.FlushText();
        }
    }
}
