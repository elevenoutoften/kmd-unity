using System.Net;
using Markdig.Renderers;
using Markdig.Syntax.Inlines;

namespace Kmd.MarkdownReader
{
    public class LiteralInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, LiteralInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, LiteralInline obj)
        {
            var text = WebUtility.HtmlEncode(obj.Content.ToString());
            renderer.WriteText(text);
        }
    }
}
