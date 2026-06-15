using Markdig.Renderers;
using Markdig.Syntax;

namespace Kmd.MarkdownReader
{
    public class ParagraphBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, ParagraphBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, ParagraphBlock obj)
        {
            // Paragraphs containing inline code or links render as a wrapping row of
            // inline segments so code is a rounded click-to-copy chip and links are
            // really clickable. Plain prose stays on the single rich-text Label (keeps
            // whole-paragraph text selection and is cheaper).
            if (InlineFlowBuilder.NeedsFlow(obj.Inline))
            {
                renderer.FlushText();
                var flow = InlineFlowBuilder.Build(obj.Inline, "md-paragraph", renderer);
                renderer.AddToCurrentBlock(flow);
                return;
            }

            var label = renderer.StartTextElement("md-paragraph");
            label.AddToClassList("md-paragraph");
            renderer.WriteChildren(obj.Inline);
            renderer.FlushText();
        }
    }
}
