using Markdig.Renderers;
using Markdig.Syntax;

namespace Kmd.MarkdownReader
{
    public class ListBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, ListBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, ListBlock obj)
        {
            renderer.WriteChildren(obj);
        }
    }
}
