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

            var label = new Label { name = "md-codeblock" };
            label.AddToClassList("md-codeblock");

            if (language != null)
            {
                // Syntax-highlighted: rich text with <color> runs from ColorCode.
                label.enableRichText = true;
                label.text = new ColorCodeRichTextFormatter().GetRichText(codeText, language);
            }
            else
            {
                // Unknown/no language: plain monospace, no highlighting.
                label.enableRichText = false;
                label.text = codeText;
            }

            // Copy button overlays the block (shown on hover via USS).
            label.Add(CodeBlockCopyButton.Create(codeText));
            renderer.AddToCurrentBlock(label);
        }
    }
}
