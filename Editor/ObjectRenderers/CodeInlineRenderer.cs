using Markdig.Renderers;
using Markdig.Syntax.Inlines;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    public class CodeInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, CodeInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, CodeInline obj)
        {
            renderer.FlushText();

            var label = new Label(obj.Content)
            {
                name = "md-code-inline",
                enableRichText = false,
            };
            label.AddToClassList("md-code-inline");
            label.RegisterCallback<ClickEvent>(evt =>
            {
                GUIUtility.systemCopyBuffer = obj.Content;
            });

            renderer.AddToCurrentBlock(label);
        }
    }
}
