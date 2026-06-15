using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

        private static int CountDescendants(VisualElement root)
        {
            var count = 0;
            foreach (var child in root.Children())
            {
                count += 1 + CountDescendants(child);
            }

            return count;
        }

        private static string BuildLargeDocument()
        {
            const string paragraph =
                "lorem ipsum dolor sit amet consectetur adipiscing elit sed do " +
                "lorem ipsum dolor sit amet consectetur adipiscing elit sed do " +
                "lorem ipsum dolor sit amet consectetur adipiscing elit sed do " +
                "lorem ipsum dolor sit amet consectetur adipiscing elit sed do " +
                "lorem ipsum dolor sit amet consectetur adipiscing elit sed do";

            var markdown = new StringBuilder();
            for (var i = 1; i <= 200; i++)
            {
                markdown.Append("# Heading ").Append(i).Append('\n').Append('\n');
                markdown.Append(paragraph).Append('\n').Append('\n');
            }

            return markdown.ToString();
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
        public void InlineText_NeutralizesAngleBracketForRichText()
        {
            // UI Toolkit rich text treats only '<' as markup and decodes NO HTML
            // entities, so the renderer neutralizes '<' with a trailing zero-width
            // space (U+200B) and leaves '&' (and every other character) untouched.
            var body = Render("a < b & c");
            var paragraph = body.Q<Label>(className: "md-paragraph");
            Assert.IsNotNull(paragraph);
            StringAssert.Contains("<​", paragraph.text);
            StringAssert.Contains("&", paragraph.text);
            StringAssert.DoesNotContain("&lt;", paragraph.text);
            StringAssert.DoesNotContain("&amp;", paragraph.text);
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
        public void LinkInParagraph_EmitsInlineFlow()
        {
            var body = Render("[x](https://example.com)");
            var paragraph = body.Q(className: "md-paragraph");
            Assert.IsNotNull(paragraph);
            Assert.IsTrue(paragraph.ClassListContains("md-inline-flow"));
        }

        [Test]
        public void InlineCodeInParagraph_EmitsChip()
        {
            var body = Render("`code`");
            var paragraph = body.Q(className: "md-paragraph");
            Assert.IsNotNull(paragraph);
            Assert.IsNotNull(paragraph.Q(className: "md-code-inline"));
        }

        [Test]
        public void MixedInlineContent_EmitsRunsAndChips()
        {
            var body = Render("text `code` more");
            var paragraph = body.Q(className: "md-paragraph");
            Assert.IsNotNull(paragraph);
            Assert.IsNotNull(paragraph.Q(className: "md-inline-run"));
            Assert.IsNotNull(paragraph.Q(className: "md-code-inline"));
        }

        [Test]
        public void BlockedLinkScheme_RendersNoLinkTag()
        {
            var body = Render("[x](javascript:alert(1))");
            var paragraph = body.Q(className: "md-paragraph");
            Assert.IsNotNull(paragraph);
            var labels = paragraph.Query<Label>().ToList();
            Assert.IsFalse(labels.Any(label => !string.IsNullOrEmpty(label.text) && label.text.Contains("<link=")));
        }

        [Test]
        public void RichTextLink_EmitsLinkTag()
        {
            var body = Render("[x](https://example.com)");
            var paragraph = body.Q(className: "md-paragraph");
            Assert.IsNotNull(paragraph);
            Assert.IsTrue(paragraph.ClassListContains("md-inline-flow"));

            var run = paragraph.Query<Label>(className: "md-inline-run").ToList().FirstOrDefault(label => label.text.Contains("<link="));
            Assert.IsNotNull(run);
            StringAssert.Contains("<link=", run.text);
        }

        [Test]
        public void BlockedLinkInFlow_NoLinkTag()
        {
            var body = Render("[x](javascript:alert(1))");
            var paragraph = body.Q(className: "md-paragraph");
            Assert.IsNotNull(paragraph);

            var labels = paragraph.Query<Label>().ToList();
            Assert.IsFalse(labels.Any(label => !string.IsNullOrEmpty(label.text) && label.text.Contains("<link=")));
        }

        [Test]
        public void LargeDocument_ElementCountBudget()
        {
            var body = Render(BuildLargeDocument());
            Assert.Less(CountDescendants(body), 3000);
        }

        [Test]
        public void LargeDocument_RenderTimeBudget()
        {
            if (Type.GetType("System.Diagnostics.Stopwatch") == null)
            {
                Assert.Ignore("Stopwatch not available");
            }

            var markdown = BuildLargeDocument();
            Render(markdown); // warm shared pipeline/JIT to reduce test-runner noise
            var stopwatch = Stopwatch.StartNew();
            Render(markdown);
            stopwatch.Stop();

            Assert.Less(stopwatch.ElapsedMilliseconds, 500);
        }
    }
}
