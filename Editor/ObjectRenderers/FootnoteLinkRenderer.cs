using Markdig.Extensions.Footnotes;
using Markdig.Renderers;
using UnityEditor;

namespace Kmd.MarkdownReader
{
    // The in-text footnote reference (superscript [n]) and the return arrow in the
    // footnote definition.
    public class FootnoteLinkRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, FootnoteLink>
    {
        protected override void Write(UIMarkdownRenderer renderer, FootnoteLink obj)
        {
            if (obj.IsBackLink)
            {
                renderer.WriteText(" <size=80%>↩</size>");
                return;
            }

            var order = obj.Footnote != null ? obj.Footnote.Order : obj.Index;
            var color = EditorGUIUtility.isProSkin ? "#9b6dff" : "#7c4dff";
            // UI Toolkit has no <sup>; approximate a superscript footnote ref with a
            // smaller, link-colored number (matches kmd's purple footnote marks).
            renderer.WriteText("<link=\"#fn-" + order + "\"><size=70%><color=" + color + ">" + order + "</color></size></link>");
        }
    }
}
