using Markdig.Extensions.Footnotes;
using Markdig.Renderers;

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
            // UI Toolkit has no <sup>; approximate superscript with a smaller size.
            renderer.WriteText("<link=\"#fn-" + order + "\"><size=70%>[" + order + "]</size></link>");
        }
    }
}
