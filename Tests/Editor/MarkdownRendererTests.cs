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

        private static string BuildLargeInlineDocument()
        {
            var markdown = new StringBuilder();
            for (var i = 1; i <= 100; i++)
            {
                markdown
                    .Append("This paragraph shows how renderer pass ").Append(i).Append(" keeps prose together while `code").Append(i).Append("a` ")
                    .Append("and [guide").Append(i).Append("a](https://example.com/").Append(i).Append("/guide) appear inside realistic technical writing. ")
                    .Append("The sample adds enough ordinary words to expose per word label churn, references `code").Append(i).Append("b`, ")
                    .Append("then uses [note").Append(i).Append("b](https://example.com/").Append(i).Append("/note) before `code").Append(i).Append("c` closes the sentence ")
                    .Append("with measurable prose for the inline budget test.\n\n");
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
        public void OutlineEntries_TargetSeparateHeadingsWhenIdsRepeat()
        {
            var renderer = new UIMarkdownRenderer();
            renderer.Render("# First {#same}\n\n# Second {#same}");

            var entries = OutlineExtractor.Extract(renderer.Document);
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("same", entries[0].Id);
            Assert.AreEqual("same", entries[1].Id);
            Assert.AreNotEqual(entries[0].Anchor, entries[1].Anchor);

            Assert.IsTrue(renderer.TryGetOutlineHeading(entries[0].Anchor, out var first));
            Assert.IsTrue(renderer.TryGetOutlineHeading(entries[1].Anchor, out var second));
            Assert.AreNotSame(first, second);

            Assert.IsTrue(renderer.TryGetHeading("same", out var fragmentTarget));
            Assert.AreSame(first, fragmentTarget);
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
        public void LargeInlineDocument_ElementCountBudget()
        {
            var body = Render(BuildLargeInlineDocument());
            // Coarse-run (runs + chips): ~800-1200 elements.
            // Per-word regression (one Label per word): ~3500-5000 elements.
            // Budget must be between these to FAIL under regression.
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

        [Test]
        public void LargeInlineDocument_RenderTimeBudget()
        {
            if (Type.GetType("System.Diagnostics.Stopwatch") == null)
            {
                Assert.Ignore("Stopwatch not available");
            }

            var markdown = BuildLargeInlineDocument();
            Render(markdown); // warm up
            var stopwatch = Stopwatch.StartNew();
            Render(markdown);
            stopwatch.Stop();

            Assert.Less(stopwatch.ElapsedMilliseconds, 1000);
        }
    }
}
