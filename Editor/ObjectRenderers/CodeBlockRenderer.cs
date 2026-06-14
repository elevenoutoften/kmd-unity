using Markdig.Renderers;
using Markdig.Syntax;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    public class CodeBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, CodeBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, CodeBlock obj)
        {
            renderer.FlushText();

            var codeText = obj.Lines.ToString();
            var label = new Label(codeText)
            {
                name = "md-code",
                enableRichText = false,
            };
            label.AddToClassList("md-code");
            renderer.AddToCurrentBlock(label);
        }
    }
}
