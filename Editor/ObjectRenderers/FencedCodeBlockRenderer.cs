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
            var label = new Label(codeText)
            {
                name = "md-codeblock",
                enableRichText = false,
            };
            label.AddToClassList("md-codeblock");
            renderer.AddToCurrentBlock(label);
        }
    }
}
