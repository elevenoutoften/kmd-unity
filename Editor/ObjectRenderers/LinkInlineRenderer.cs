using System;
using System.Net;
using Markdig.Renderers;
using Markdig.Syntax.Inlines;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    public class LinkInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, LinkInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, LinkInline obj)
        {
            if (obj.IsImage)
            {
                renderer.FlushText();
                var imageLabel = new Label("[Image: " + obj.Url + "]")
                {
                    name = "md-image-placeholder",
                };
                imageLabel.AddToClassList("md-image-placeholder");
                renderer.AddToCurrentBlock(imageLabel);
                return;
            }

            var url = obj.Url ?? string.Empty;
            if (url.StartsWith("#", StringComparison.Ordinal)
                || (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    && (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "mailto")))
            {
                renderer.WriteText("<link=\"" + WebUtility.HtmlEncode(url) + "\">");
                renderer.WriteChildren(obj);
                renderer.WriteText("</link>");
            }
            else
            {
                renderer.WriteChildren(obj);
            }
        }
    }
}
