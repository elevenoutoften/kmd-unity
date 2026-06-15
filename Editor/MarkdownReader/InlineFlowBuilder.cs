using System.Text;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax.Inlines;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    /// <summary>
    /// Renders a Markdig inline subtree as a wrapping row of inline segments
    /// (<c>flex-direction: row; flex-wrap: wrap</c>). This is what lets inline code be a
    /// rounded, click-to-copy chip and links be real clickable elements via the public
    /// <see cref="ClickEvent"/> — no internal link-tag events. Plain runs are split into
    /// word segments so flex-wrap reproduces natural word wrapping.
    ///
    /// Only used for inline content that actually contains a chip/link (see
    /// <see cref="NeedsFlow"/>); plain prose stays on the single rich-text Label path,
    /// which keeps whole-paragraph text selection and is cheaper. Colours are resolved
    /// from the editor skin in C# (like the inline renderers), so all themes match.
    /// </summary>
    internal static class InlineFlowBuilder
    {
        private struct Style
        {
            public bool Bold;
            public bool Italic;
            public bool Strike;
            public string LinkUrl; // non-null => clickable link
        }

        /// <summary>True if the subtree contains inline code, a link, or an autolink.</summary>
        public static bool NeedsFlow(ContainerInline inlines)
        {
            if (inlines == null)
            {
                return false;
            }

            for (var child = inlines.FirstChild; child != null; child = child.NextSibling)
            {
                // Image-only paragraphs don't need flow — keep them on the proven path.
                if (child is CodeInline || child is AutolinkInline || (child is LinkInline link && !link.IsImage))
                {
                    return true;
                }

                if (child is ContainerInline container && NeedsFlow(container))
                {
                    return true;
                }
            }

            return false;
        }

        public static VisualElement Build(ContainerInline inlines, string className, UIMarkdownRenderer renderer)
        {
            var flow = new VisualElement { name = className };
            flow.AddToClassList("md-inline-flow");
            if (!string.IsNullOrEmpty(className))
            {
                flow.AddToClassList(className);
            }

            Walk(flow, inlines, default, renderer);
            return flow;
        }

        private static void Walk(VisualElement flow, ContainerInline container, Style style, UIMarkdownRenderer renderer)
        {
            if (container == null)
            {
                return;
            }

            for (var child = container.FirstChild; child != null; child = child.NextSibling)
            {
                Emit(flow, child, style, renderer);
            }
        }

        private static void Emit(VisualElement flow, Inline inline, Style style, UIMarkdownRenderer renderer)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    EmitWords(flow, literal.Content.ToString(), style, renderer);
                    break;

                case EmphasisInline emphasis:
                {
                    var nested = style;
                    if (emphasis.DelimiterChar == '~' && emphasis.DelimiterCount == 2)
                    {
                        nested.Strike = true;
                    }
                    else if (emphasis.DelimiterCount >= 2)
                    {
                        nested.Bold = true;
                    }
                    else
                    {
                        nested.Italic = true;
                    }

                    Walk(flow, emphasis, nested, renderer);
                    break;
                }

                case CodeInline code:
                    EmitChip(flow, code.Content);
                    break;

                case LinkInline image when image.IsImage:
                    EmitImage(flow, image, renderer);
                    break;

                case LinkInline link:
                {
                    var url = link.Url ?? string.Empty;
                    var nested = style;
                    if (UrlPolicy.IsSafe(url))
                    {
                        nested.LinkUrl = url;
                    }

                    Walk(flow, link, nested, renderer); // blocked links render as plain text
                    break;
                }

                case AutolinkInline auto:
                {
                    var display = auto.Url ?? string.Empty;
                    var url = auto.IsEmail ? "mailto:" + display : display;
                    var nested = style;
                    if (UrlPolicy.IsSafe(url))
                    {
                        nested.LinkUrl = url;
                    }

                    EmitWords(flow, display, nested, renderer);
                    break;
                }

                case LineBreakInline lineBreak:
                    if (lineBreak.IsHard)
                    {
                        EmitHardBreak(flow);
                    }
                    else
                    {
                        EmitWords(flow, " ", style, renderer);
                    }

                    break;

                case FootnoteLink footnote:
                    EmitFootnote(flow, footnote, renderer);
                    break;

                case ContainerInline nested:
                    Walk(flow, nested, style, renderer);
                    break;

                // Unknown leaf inlines (raw HTML, entities, math, ...) are left out, the
                // same as the rich-text path which has no renderer registered for them.
            }
        }

        private static void EmitWords(VisualElement flow, string text, Style style, UIMarkdownRenderer renderer)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Split into atomic segments (a run of non-space chars plus any trailing
            // whitespace) so flex-wrap can break between them like word wrapping. The
            // trailing space rides along on each segment so spacing survives wrapping.
            var i = 0;
            while (i < text.Length)
            {
                var start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                EmitSegment(flow, text.Substring(start, i - start), style, renderer);
            }
        }

        private static void EmitSegment(VisualElement flow, string token, Style style, UIMarkdownRenderer renderer)
        {
            var label = new Label { enableRichText = true };
            label.AddToClassList("md-inline-word");

            var escaped = UIMarkdownRenderer.EscapeRichText(token);
            if (style.Bold || style.Italic || style.Strike)
            {
                var sb = new StringBuilder();
                if (style.Bold) sb.Append("<b>");
                if (style.Italic) sb.Append("<i>");
                if (style.Strike) sb.Append("<s>");
                sb.Append(escaped);
                if (style.Strike) sb.Append("</s>");
                if (style.Italic) sb.Append("</i>");
                if (style.Bold) sb.Append("</b>");
                label.text = sb.ToString();
            }
            else
            {
                label.text = escaped;
            }

            if (style.LinkUrl != null)
            {
                label.AddToClassList("md-inline-link");
                label.style.color = LinkColor();
                var url = style.LinkUrl;
                label.RegisterCallback<ClickEvent>(_ => LinkActivation.Activate(url, renderer));
            }

            flow.Add(label);
        }

        private static void EmitChip(VisualElement flow, string content)
        {
            var chip = new Label(content) { enableRichText = false };
            chip.AddToClassList("md-code-inline");
            chip.style.color = CodeForeground();
            chip.style.backgroundColor = CodeBackground();
            chip.tooltip = "Click to copy";
            chip.RegisterCallback<ClickEvent>(_ =>
            {
                GUIUtility.systemCopyBuffer = content;
                chip.tooltip = "Copied!";
                chip.schedule.Execute(() => chip.tooltip = "Click to copy").StartingIn(1000);
            });

            flow.Add(chip);
        }

        private static void EmitImage(VisualElement flow, LinkInline link, UIMarkdownRenderer renderer)
        {
            var image = new Image { name = "md-image", scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList("md-image");
            flow.Add(image);
            ImageLoader.Load(image, link.Url ?? string.Empty, renderer.BaseDirectory);
        }

        private static void EmitFootnote(VisualElement flow, FootnoteLink footnote, UIMarkdownRenderer renderer)
        {
            if (footnote.IsBackLink)
            {
                return; // the return arrow only appears inside footnote definitions
            }

            var order = footnote.Footnote != null ? footnote.Footnote.Order : footnote.Index;
            var label = new Label("<size=70%>" + order + "</size>") { enableRichText = true };
            label.AddToClassList("md-inline-word");
            label.AddToClassList("md-inline-link");
            label.style.color = LinkColor();
            var target = "#fn-" + order;
            label.RegisterCallback<ClickEvent>(_ => LinkActivation.Activate(target, renderer));
            flow.Add(label);
        }

        private static void EmitHardBreak(VisualElement flow)
        {
            var lineBreak = new VisualElement { name = "md-inline-break" };
            lineBreak.AddToClassList("md-inline-break"); // flex-basis: 100% forces a wrap
            flow.Add(lineBreak);
        }

        private static Color LinkColor() => Hex(EditorGUIUtility.isProSkin ? "#9b6dff" : "#7c4dff");

        private static Color CodeForeground() => Hex(EditorGUIUtility.isProSkin ? "#9b6dff" : "#7c4dff");

        private static Color CodeBackground() => Hex(EditorGUIUtility.isProSkin ? "#2c2f35" : "#eceff3");

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
