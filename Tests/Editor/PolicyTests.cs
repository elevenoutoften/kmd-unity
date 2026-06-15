using System.IO;
using System.Text;
using NUnit.Framework;

namespace Kmd.MarkdownReader.Tests
{
    // Pure-logic coverage for the security/escaping helpers that gate the riskiest
    // behavior (link/image classification and rich-text neutralization). These need no
    // UI Toolkit tree, so they run fast and deterministically.
    public class PolicyTests
    {
        [TestCase("#section", UrlKind.Fragment)]
        [TestCase("https://example.com", UrlKind.External)]
        [TestCase("http://example.com", UrlKind.External)]
        [TestCase("mailto:a@b.com", UrlKind.External)]
        [TestCase("./docs/x.md", UrlKind.Relative)]
        [TestCase("images/y.png", UrlKind.Relative)]
        [TestCase("javascript:alert(1)", UrlKind.Blocked)]
        [TestCase("vbscript:msgbox", UrlKind.Blocked)]
        [TestCase("data:text/html;base64,AAAA", UrlKind.Blocked)]
        [TestCase("file:///etc/passwd", UrlKind.Blocked)]
        [TestCase("", UrlKind.Blocked)]
        public void UrlPolicy_Classify(string url, UrlKind expected)
        {
            Assert.AreEqual(expected, UrlPolicy.Classify(url));
        }

        [Test]
        public void UrlPolicy_IsSafe_MatchesClassification()
        {
            Assert.IsTrue(UrlPolicy.IsSafe("https://example.com"));
            Assert.IsTrue(UrlPolicy.IsSafe("#frag"));
            Assert.IsTrue(UrlPolicy.IsSafe("./rel.md"));
            Assert.IsFalse(UrlPolicy.IsSafe("javascript:alert(1)"));
            Assert.IsFalse(UrlPolicy.IsSafe("data:text/html,x"));
        }

        [TestCase("search:logo", ImageSourceKind.Asset)]
        [TestCase("http://example.com/a.png", ImageSourceKind.Remote)]
        [TestCase("https://example.com/a.png", ImageSourceKind.Remote)]
        [TestCase("./local.png", ImageSourceKind.Local)]
        [TestCase("img/local.png", ImageSourceKind.Local)]
        [TestCase("C:\\images\\local.png", ImageSourceKind.Local)]
        [TestCase("data:image/png;base64,AAAA", ImageSourceKind.Invalid)]
        [TestCase("javascript:alert(1)", ImageSourceKind.Invalid)]
        [TestCase("file:///etc/passwd", ImageSourceKind.Invalid)]
        [TestCase("ftp://example.com/a.png", ImageSourceKind.Invalid)]
        [TestCase("", ImageSourceKind.Invalid)]
        public void ImagePolicy_Classify(string url, ImageSourceKind expected)
        {
            Assert.AreEqual(expected, ImagePolicy.Classify(url));
        }

        [Test]
        public void EscapeRichText_NeutralizesAngleBracketOnly()
        {
            Assert.AreEqual("a <​ b", UIMarkdownRenderer.EscapeRichText("a < b"));
        }

        [Test]
        public void EscapeRichText_LeavesAmpersandAndOthersUntouched()
        {
            Assert.AreEqual("a & b > c \" d", UIMarkdownRenderer.EscapeRichText("a & b > c \" d"));
        }

        [Test]
        public void EscapeRichText_ReturnsSameInstanceWhenNoMarkup()
        {
            // The no-angle-bracket fast path should avoid allocating a new string.
            var input = "plain text with no markup";
            Assert.AreSame(input, UIMarkdownRenderer.EscapeRichText(input));
        }

        [Test]
        public void AppendEscaped_MatchesEscapeRichText()
        {
            var builder = new StringBuilder();
            const string source = "x <tag> y";
            UIMarkdownRenderer.AppendEscaped(builder, source, 0, source.Length);
            Assert.AreEqual(UIMarkdownRenderer.EscapeRichText(source), builder.ToString());
        }

        [Test]
        public void AppendEscaped_HonorsSliceBounds()
        {
            var builder = new StringBuilder();
            const string source = "abc<def";
            UIMarkdownRenderer.AppendEscaped(builder, source, 3, 1); // just the '<'
            Assert.AreEqual("<​", builder.ToString());
        }

        [Test]
        public void LinkAttribute_EscapesTagBreakingCharacters()
        {
            Assert.AreEqual("a%22b%3Cc%3Ed", LinkInlineRenderer.LinkAttribute("a\"b<c>d"));
        }

        [Test]
        public void LinkActivation_IgnoresNullEmptyAndBlocked()
        {
            // No exception, no side effect (blocked schemes never reach OpenURL).
            Assert.DoesNotThrow(() => LinkActivation.Activate(null, null));
            Assert.DoesNotThrow(() => LinkActivation.Activate(string.Empty, null));
            Assert.DoesNotThrow(() => LinkActivation.Activate("javascript:alert(1)", null));
        }

        [Test]
        public void LinkActivation_UnknownFragmentIsSafe()
        {
            var renderer = new UIMarkdownRenderer();
            renderer.Render("# Hello");
            // A fragment with no matching heading resolves to a no-op (no ScrollTo).
            Assert.DoesNotThrow(() => LinkActivation.Activate("#no-such-anchor", renderer));
        }

        [Test]
        public void ImagePolicy_BlocksOutOfProjectPathUnlessOptedIn()
        {
            var original = ImagePolicy.AllowExternalImages;
            try
            {
                // A path clearly outside the project and outside any document directory.
                var outside = Path.Combine(Path.GetTempPath(), "kmd-not-in-project.png");

                ImagePolicy.AllowExternalImages = false;
                Assert.IsFalse(ImagePolicy.IsAllowedLocalPath(outside, null),
                    "out-of-project path must be blocked when external images are off");

                ImagePolicy.AllowExternalImages = true;
                Assert.IsTrue(ImagePolicy.IsAllowedLocalPath(outside, null),
                    "opt-in must allow external paths");
            }
            finally
            {
                ImagePolicy.AllowExternalImages = original;
            }
        }

        [Test]
        public void ImagePolicy_AllowsPathsUnderDocumentDirectory()
        {
            var original = ImagePolicy.AllowExternalImages;
            try
            {
                ImagePolicy.AllowExternalImages = false;
                var baseDir = Path.GetTempPath();
                var underBase = Path.Combine(baseDir, "img", "pic.png");
                Assert.IsTrue(ImagePolicy.IsAllowedLocalPath(underBase, baseDir),
                    "paths under the document directory load without opt-in");

                // A '..' escape out of the document directory must NOT be allowed.
                var escape = Path.Combine(baseDir, "..", "..", "secret.png");
                Assert.IsFalse(ImagePolicy.IsAllowedLocalPath(escape, baseDir),
                    "'..' traversal out of the document directory must be blocked");
            }
            finally
            {
                ImagePolicy.AllowExternalImages = original;
            }
        }
    }
}
