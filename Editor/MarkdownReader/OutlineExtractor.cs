using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Kmd.MarkdownReader
{
    public struct OutlineEntry
    {
        public int Level;
        public string Text;
        public string Id;
        public string Anchor;
    }

    /// <summary>Walks a parsed document and extracts its heading tree.</summary>
    public static class OutlineExtractor
    {
        private const string AnchorPrefix = "kmd-outline-heading-";

        public static List<OutlineEntry> Extract(MarkdownDocument document)
        {
            var entries = new List<OutlineEntry>();
            if (document == null)
            {
                return entries;
            }

            var index = 0;
            foreach (var heading in document.Descendants().OfType<HeadingBlock>())
            {
                entries.Add(new OutlineEntry
                {
                    Level = heading.Level,
                    Text = GetInlineText(heading.Inline),
                    Id = heading.GetAttributes().Id,
                    Anchor = CreateAnchor(index++),
                });
            }

            return entries;
        }

        internal static string CreateAnchor(int index)
        {
            return AnchorPrefix + index.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetInlineText(ContainerInline inline)
        {
            if (inline == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var node in inline.Descendants())
            {
                if (node is LiteralInline literal)
                {
                    sb.Append(literal.Content.ToString());
                }
                else if (node is CodeInline code)
                {
                    sb.Append(code.Content);
                }
            }

            return sb.ToString();
        }
    }
}
