using Markdig.Extensions.Footnotes;
using Markdig.Renderers;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    // The footnote section appended at the bottom of the document.
    public class FootnoteGroupRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, FootnoteGroup>
    {
        protected override void Write(UIMarkdownRenderer renderer, FootnoteGroup group)
        {
            renderer.FlushText();

            var section = new VisualElement { name = "md-footnotes" };
            section.AddToClassList("md-footnotes");
            renderer.StartBlock(section);

            var rule = new VisualElement { name = "md-footnotes-rule" };
            rule.AddToClassList("md-footnotes-rule");
            renderer.AddToCurrentBlock(rule);

            foreach (var child in group)
            {
                if (!(child is Footnote footnote))
                {
                    continue;
                }

                var item = new VisualElement { name = "md-footnote" };
                item.AddToClassList("md-footnote");
                renderer.StartBlock(item);
                // Reuse the id registry so the in-text reference can scroll here.
                renderer.RegisterHeading("fn-" + footnote.Order, item);

                var num = new Label(footnote.Order + ".") { name = "md-footnote-num" };
                num.AddToClassList("md-footnote-num");
                renderer.AddToCurrentBlock(num);

                var content = new VisualElement { name = "md-footnote-content" };
                content.AddToClassList("md-footnote-content");
                renderer.StartBlock(content);
                renderer.WriteChildren(footnote);
                renderer.FinishBlock(); // content

                renderer.FinishBlock(); // item
            }

            renderer.FinishBlock(); // section
        }
    }
}
