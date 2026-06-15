using Markdig.Renderers;
using Markdig.Syntax;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    public class FencedCodeBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, FencedCodeBlock>
    {
        private const int MaxInlineLineLength = 80;
        private const int MaxHighlightLines = 500;

        protected override void Write(UIMarkdownRenderer renderer, FencedCodeBlock obj)
        {
            renderer.FlushText();

            var codeText = obj.Lines.ToString();
            var language = LanguageMap.Resolve(obj.Info);
            var needsHorizontalScroll = HasLongLine(codeText, MaxInlineLineLength);
            var shouldHighlight = language != null && CountLines(codeText) <= MaxHighlightLines;

            // Container (box styling + positioning context for the copy button).
            // The code Label must NOT hold the button as a child — a Label is a
            // text element and a child element breaks its height, overlapping the
            // following content.
            var container = new VisualElement { name = "md-codeblock" };
            container.AddToClassList("md-codeblock");

            var label = new Label { name = "md-codeblock-text" };
            label.AddToClassList("md-codeblock-text");

            if (shouldHighlight)
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

            if (needsHorizontalScroll)
            {
                // Long lines scroll horizontally instead of stretching the document
                // (mirrors the GFM table wrapper).
                var scroll = new ScrollView(ScrollViewMode.Horizontal) { name = "md-codeblock-scroll" };
                scroll.AddToClassList("md-codeblock-scroll");
                scroll.Add(label);
                container.Add(scroll);
            }
            else
            {
                container.Add(label);
            }

            container.Add(CodeBlockCopyButton.Create(codeText));
            renderer.AddToCurrentBlock(container);
        }

        private static bool HasLongLine(string text, int threshold)
        {
            var lineLength = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch == '\r')
                {
                    continue;
                }

                if (ch == '\n')
                {
                    if (lineLength > threshold)
                    {
                        return true;
                    }

                    lineLength = 0;
                    continue;
                }

                lineLength++;
            }

            return lineLength > threshold;
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var lineCount = 1;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    lineCount++;
                }
            }

            if (text[text.Length - 1] == '\n')
            {
                lineCount--;
            }

            return lineCount;
        }
    }
}
