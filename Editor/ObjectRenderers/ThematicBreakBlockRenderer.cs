using Markdig.Renderers;
using Markdig.Syntax;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    public class ThematicBreakBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, ThematicBreakBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, ThematicBreakBlock obj)
        {
            renderer.FlushText();
            var hr = new VisualElement { name = "md-hr" };
            hr.AddToClassList("md-hr");
            renderer.AddToCurrentBlock(hr);
        }
    }
}
