using Markdig.Renderers;
using Markdig.Syntax;

namespace Kmd.MarkdownReader
{
    public class ParagraphBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, ParagraphBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, ParagraphBlock obj)
        {
            var label = renderer.StartTextElement("md-paragraph");
            label.AddToClassList("md-paragraph");
            renderer.WriteChildren(obj.Inline);
            renderer.FlushText();
        }
    }
}
