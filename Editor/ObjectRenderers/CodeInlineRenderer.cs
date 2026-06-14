using Markdig.Renderers;
using Markdig.Syntax.Inlines;
using UnityEditor;

namespace Kmd.MarkdownReader
{
    public class CodeInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, CodeInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, CodeInline obj)
        {
            // Inline code must stay part of the surrounding text flow. A separate
            // Label is a flex child, so it dropped onto its own line and pushed the
            // following text down. Emit it as rich text into the current paragraph
            // label instead. UI Toolkit can't pad a mono chip inline, so approximate
            // kmd's inline-code styling with a tinted <mark> highlight in the
            // tertiary color.
            var dark = EditorGUIUtility.isProSkin;
            var foreground = dark ? "#9b6dff" : "#7c4dff";
            var background = dark ? "#2c2f35ff" : "#eceff3ff";

            renderer.WriteText("<mark=" + background + "><color=" + foreground + ">");
            renderer.WriteText(UIMarkdownRenderer.EscapeRichText(obj.Content));
            renderer.WriteText("</color></mark>");
        }
    }
}
