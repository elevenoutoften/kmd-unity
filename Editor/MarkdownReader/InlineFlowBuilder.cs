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
    /// rounded, click-to-copy chip while rich-text links continue through the shared
    /// link-tag activation path. Plain text is emitted as coarse rich-text runs so a
    /// paragraph does not allocate one <see cref="Label"/> per word.
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
            public string LinkUrl; // non-null => rich-text link
        }

        private sealed class RunBuilder
        {
            private readonly VisualElement _flow;
            private readonly StringBuilder _text = new StringBuilder();

            public RunBuilder(VisualElement flow)
            {
                _flow = flow;
            }

            public void Append(string text, Style style)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                if (style.LinkUrl != null)
                {
                    _text.Append("<link=\"");
                    _text.Append(LinkInlineRenderer.LinkAttribute(style.LinkUrl));
                    _text.Append("\"><color=");
                    _text.Append(LinkColorText());
                    _text.Append(">");
                }

                if (style.Bold) _text.Append("<b>");
                if (style.Italic) _text.Append("<i>");
                if (style.Strike) _text.Append("<s>");

                UIMarkdownRenderer.AppendEscaped(_text, text, 0, text.Length);

                if (style.Strike) _text.Append("</s>");
                if (style.Italic) _text.Append("</i>");
                if (style.Bold) _text.Append("</b>");

                if (style.LinkUrl != null)
                {
                    _text.Append("</color></link>");
                }
            }

            public void Flush()
            {
                if (_text.Length == 0)
                {
                    return;
                }

                var label = new Label
                {
                    enableRichText = true,
                    text = _text.ToString(),
                };
                label.AddToClassList("md-inline-run");
                UIMarkdownRenderer.MakeLabelSelectable(label);
                _flow.Add(label);
                _text.Clear();
            }
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

            var run = new RunBuilder(flow);
            Walk(flow, inlines, default, renderer, run);
            run.Flush();
            return flow;
        }

        private static void Walk(
            VisualElement flow,
            ContainerInline container,
            Style style,
            UIMarkdownRenderer renderer,
            RunBuilder run)
        {
            if (container == null)
            {
                return;
            }

            for (var child = container.FirstChild; child != null; child = child.NextSibling)
            {
                Emit(flow, child, style, renderer, run);
            }
        }

        private static void Emit(
            VisualElement flow,
            Inline inline,
            Style style,
            UIMarkdownRenderer renderer,
            RunBuilder run)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    run.Append(literal.Content.ToString(), style);
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

                    Walk(flow, emphasis, nested, renderer, run);
                    break;
                }

                case CodeInline code:
                    run.Flush();
                    EmitChip(flow, code.Content);
                    break;

                case LinkInline image when image.IsImage:
                    run.Flush();
                    EmitImage(flow, image, renderer);
                    break;

                case LinkInline link:
                {
                    var url = link.Url ?? string.Empty;
                    if (UrlPolicy.IsSafe(url))
                    {
                        if (renderer.RichTextLinksClickable)
                        {
                            var nested = style;
                            nested.LinkUrl = url;
                            Walk(flow, link, nested, renderer, run);
                        }
                        else
                        {
                            run.Flush();
                            EmitLinkLabel(flow, link, style, url, renderer);
                        }

                        break;
                    }

                    Walk(flow, link, style, renderer, run); // blocked links render as plain text
                    break;
                }

                case AutolinkInline auto:
                {
                    var display = auto.Url ?? string.Empty;
                    var url = auto.IsEmail ? "mailto:" + display : display;
                    if (UrlPolicy.IsSafe(url))
                    {
                        if (renderer.RichTextLinksClickable)
                        {
                            var nested = style;
                            nested.LinkUrl = url;
                            run.Append(display, nested);
                        }
                        else
                        {
                            run.Flush();
                            EmitLinkLabel(flow, display, style, url, renderer);
                        }

                        break;
                    }

                    run.Append(display, style);
                    break;
                }

                case LineBreakInline lineBreak:
                    if (lineBreak.IsHard)
                    {
                        run.Flush();
                        EmitHardBreak(flow);
                    }
                    else
                    {
                        run.Append(" ", style);
                    }

                    break;

                case FootnoteLink footnote:
                    run.Flush();
                    EmitFootnote(flow, footnote, renderer);
                    break;

                case ContainerInline nested:
                    Walk(flow, nested, style, renderer, run);
                    break;

                // Unknown leaf inlines (raw HTML, entities, math, ...) are left out, the
                // same as the rich-text path which has no renderer registered for them.
            }
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

        private static void EmitLinkLabel(
            VisualElement flow,
            ContainerInline container,
            Style style,
            string url,
            UIMarkdownRenderer renderer)
        {
            var text = new StringBuilder();
            AppendInlineText(text, container, style);
            EmitLinkLabel(flow, text.ToString(), url, renderer);
        }

        private static void EmitLinkLabel(
            VisualElement flow,
            string text,
            Style style,
            string url,
            UIMarkdownRenderer renderer)
        {
            var richText = new StringBuilder();
            AppendFormatted(richText, text, style);
            EmitLinkLabel(flow, richText.ToString(), url, renderer);
        }

        private static void EmitLinkLabel(VisualElement flow, string text, string url, UIMarkdownRenderer renderer)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var label = new Label(text) { enableRichText = true };
            label.AddToClassList("md-inline-link");
            label.style.color = LinkColor();
            label.RegisterCallback<ClickEvent>(_ => LinkActivation.Activate(url, renderer));
            flow.Add(label);
        }

        private static void AppendInlineText(StringBuilder text, ContainerInline container, Style style)
        {
            if (container == null)
            {
                return;
            }

            for (var child = container.FirstChild; child != null; child = child.NextSibling)
            {
                switch (child)
                {
                    case LiteralInline literal:
                        AppendFormatted(text, literal.Content.ToString(), style);
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

                        AppendInlineText(text, emphasis, nested);
                        break;
                    }

                    case CodeInline code:
                        AppendFormatted(text, code.Content, style);
                        break;

                    case AutolinkInline auto:
                        AppendFormatted(text, auto.Url ?? string.Empty, style);
                        break;

                    case ContainerInline nested:
                        AppendInlineText(text, nested, style);
                        break;
                }
            }
        }

        private static void AppendFormatted(StringBuilder text, string value, Style style)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            if (style.Bold) text.Append("<b>");
            if (style.Italic) text.Append("<i>");
            if (style.Strike) text.Append("<s>");

            UIMarkdownRenderer.AppendEscaped(text, value, 0, value.Length);

            if (style.Strike) text.Append("</s>");
            if (style.Italic) text.Append("</i>");
            if (style.Bold) text.Append("</b>");
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

        private static string LinkColorText() => EditorGUIUtility.isProSkin ? "#9b6dff" : "#7c4dff";

        private static Color CodeForeground() => Hex(EditorGUIUtility.isProSkin ? "#9b6dff" : "#7c4dff");

        private static Color CodeBackground() => Hex(EditorGUIUtility.isProSkin ? "#2c2f35" : "#eceff3");

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
