using NUnit.Framework;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader.Tests
{
    public class MarkdownRendererTests
    {
        private static VisualElement Render(string markdown)
        {
            var renderer = new UIMarkdownRenderer();
            renderer.Render(markdown);
            return renderer.ContentElement;
        }

        [Test]
        public void Heading_RendersLevelClass()
        {
            var body = Render("# Hello");
            Assert.IsNotNull(body.Q(className: "md-h1"), "expected an md-h1 element");
        }

        [Test]
        public void HeadingAndParagraph_BothRender()
        {
            var body = Render("# Hello\n\nWorld");
            Assert.IsNotNull(body.Q(className: "md-h1"));
            Assert.IsNotNull(body.Q(className: "md-paragraph"));
        }

        [Test]
        public void Bold_EmitsRichTextTag()
        {
            var body = Render("**bold**");
            var paragraph = body.Q<Label>(className: "md-paragraph");
            Assert.IsNotNull(paragraph);
            StringAssert.Contains("<b>", paragraph.text);
        }

        [Test]
        public void Strikethrough_EmitsRichTextTag()
        {
            var body = Render("~~gone~~");
            var paragraph = body.Q<Label>(className: "md-paragraph");
            Assert.IsNotNull(paragraph);
            StringAssert.Contains("<s>", paragraph.text);
        }

        [Test]
        public void InlineText_IsHtmlEscaped()
        {
            var body = Render("a < b & c");
            var paragraph = body.Q<Label>(className: "md-paragraph");
            Assert.IsNotNull(paragraph);
            StringAssert.Contains("&lt;", paragraph.text);
            StringAssert.Contains("&amp;", paragraph.text);
        }

        [Test]
        public void UnorderedList_RendersMarker()
        {
            var body = Render("- one\n- two");
            Assert.IsNotNull(body.Q(className: "md-list"));
            Assert.IsNotNull(body.Q(className: "md-list-marker"));
        }

        [Test]
        public void Blockquote_Renders()
        {
            var body = Render("> quoted");
            Assert.IsNotNull(body.Q(className: "md-blockquote"));
        }

        [Test]
        public void Alert_RendersKindClass()
        {
            var body = Render("> [!NOTE]\n> heads up");
            Assert.IsNotNull(body.Q(className: "md-alert-note"));
        }

        [Test]
        public void Table_RendersHeaderCell()
        {
            var body = Render("| a | b |\n|---|---|\n| 1 | 2 |");
            Assert.IsNotNull(body.Q(className: "md-table"));
            Assert.IsNotNull(body.Q(className: "md-th"));
        }

        [Test]
        public void FencedCode_Renders()
        {
            var body = Render("```\ncode\n```");
            Assert.IsNotNull(body.Q(className: "md-codeblock"));
        }

        [Test]
        public void ThematicBreak_Renders()
        {
            var body = Render("a\n\n---\n\nb");
            Assert.IsNotNull(body.Q(className: "md-hr"));
        }

        [Test]
        public void EmptyInput_ShowsEmptyState()
        {
            var body = Render(string.Empty);
            Assert.IsNotNull(body.Q(className: "md-empty"));
        }

        [Test]
        public void BlockedLinkScheme_RendersNoLinkTag()
        {
            var body = Render("[x](javascript:alert(1))");
            var paragraph = body.Q<Label>(className: "md-paragraph");
            Assert.IsNotNull(paragraph);
            StringAssert.DoesNotContain("<link", paragraph.text);
        }

        [Test]
        public void SafeLink_EmitsLinkTag()
        {
            var body = Render("[x](https://example.com)");
            var paragraph = body.Q<Label>(className: "md-paragraph");
            Assert.IsNotNull(paragraph);
            StringAssert.Contains("<link", paragraph.text);
        }
    }
}
