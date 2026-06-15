using Markdig.Renderers;
using Markdig.Syntax;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    public class FencedCodeBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, FencedCodeBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, FencedCodeBlock obj)
        {
            renderer.FlushText();

            var codeText = obj.Lines.ToString();
            var language = LanguageMap.Resolve(obj.Info);

            // Container (box styling + positioning context for the copy button).
            // The code Label must NOT hold the button as a child — a Label is a
            // text element and a child element breaks its height, overlapping the
            // following content.
            var container = new VisualElement { name = "md-codeblock" };
            container.AddToClassList("md-codeblock");

            // Long lines scroll horizontally instead of stretching the document
            // (mirrors the GFM table wrapper).
            var scroll = new ScrollView(ScrollViewMode.Horizontal) { name = "md-codeblock-scroll" };
            scroll.AddToClassList("md-codeblock-scroll");

            var label = new Label { name = "md-codeblock-text" };
            label.AddToClassList("md-codeblock-text");

            if (language != null)
            {
                // Syntax-highlighted: rich text with <color> runs from ColorCode.
                label.enableRichText = true;
                label.text = ColorCodeRichTextFormatter.ForCurrentSkin().GetRichText(codeText, language);
            }
            else
            {
                // Unknown/no language: plain monospace, no highlighting.
                label.enableRichText = false;
                label.text = codeText;
            }

            scroll.Add(label);
            container.Add(scroll);
            container.Add(CodeBlockCopyButton.Create(codeText));
            renderer.AddToCurrentBlock(container);
        }
    }
}
