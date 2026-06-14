using System.Net;
using Markdig.Renderers;
using Markdig.Syntax.Inlines;
using UnityEditor;
using UnityEngine;
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
                var image = new Image { name = "md-image", scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("md-image");
                renderer.AddToCurrentBlock(image);
                ImageLoader.Load(image, obj.Url ?? string.Empty, renderer.BaseDirectory);
                return;
            }

            var url = obj.Url ?? string.Empty;
            if (UrlPolicy.IsSafe(url))
            {
                var color = EditorGUIUtility.isProSkin ? "#58A6FF" : "#0969DA";
                renderer.WriteText("<link=\"" + LinkAttribute(url) + "\"><color=" + color + ">");
                renderer.WriteChildren(obj);
                renderer.WriteText("</color></link>");
            }
            else
            {
                // Blocked scheme (javascript:, data:, ...) -> render the text only.
                renderer.WriteChildren(obj);
            }
        }

        // Escape only the characters that would break the <link="..."> tag; the
        // result stays a valid URL (percent-encoded) so it round-trips to the
        // click handler unchanged.
        internal static string LinkAttribute(string url)
        {
            return url.Replace("\"", "%22").Replace("<", "%3C").Replace(">", "%3E");
        }
    }
}
