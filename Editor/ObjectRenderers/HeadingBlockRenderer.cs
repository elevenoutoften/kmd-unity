using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Kmd.MarkdownReader
{
    public class HeadingBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, HeadingBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, HeadingBlock obj)
        {
            var label = renderer.StartTextElement("md-h" + obj.Level);
            label.AddToClassList("md-h" + obj.Level);

            renderer.WriteChildren(obj.Inline);
            renderer.FlushText();

            var attributes = obj.GetAttributes();
            renderer.RegisterOutlineHeading(attributes.Id, label);
        }
    }
}
