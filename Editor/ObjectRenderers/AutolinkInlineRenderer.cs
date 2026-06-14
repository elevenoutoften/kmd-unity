using System.Net;
using Markdig.Renderers;
using Markdig.Syntax.Inlines;
using UnityEditor;

namespace Kmd.MarkdownReader
{
    // Bare URLs / emails produced by the AutoLinks extension (e.g. <https://x>
    // or an autodetected https://x). Rendered as clickable links.
    public class AutolinkInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, AutolinkInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, AutolinkInline obj)
        {
            var display = obj.Url ?? string.Empty;
            var url = obj.IsEmail ? "mailto:" + display : display;

            if (UrlPolicy.IsSafe(url))
            {
                var color = EditorGUIUtility.isProSkin ? "#58A6FF" : "#0969DA";
                renderer.WriteText("<link=\"" + LinkInlineRenderer.LinkAttribute(url) + "\"><color=" + color + ">");
                renderer.WriteText(WebUtility.HtmlEncode(display));
                renderer.WriteText("</color></link>");
            }
            else
            {
                renderer.WriteText(WebUtility.HtmlEncode(display));
            }
        }
    }
}
