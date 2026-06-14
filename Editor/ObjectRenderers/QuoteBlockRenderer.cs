using Markdig.Renderers;
using Markdig.Syntax;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    public class QuoteBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, QuoteBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, QuoteBlock obj)
        {
            renderer.FlushText();
            var quote = new VisualElement { name = "md-blockquote" };
            quote.AddToClassList("md-blockquote");
            renderer.StartBlock(quote);
            renderer.WriteChildren(obj);
            renderer.FinishBlock();
        }
    }
}
