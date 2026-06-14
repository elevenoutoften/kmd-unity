using System.Collections.Generic;
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
    }

    /// <summary>Walks a parsed document and extracts its heading tree.</summary>
    public static class OutlineExtractor
    {
        public static List<OutlineEntry> Extract(MarkdownDocument document)
        {
            var entries = new List<OutlineEntry>();
            if (document == null)
            {
                return entries;
            }

            foreach (var heading in document.Descendants().OfType<HeadingBlock>())
            {
                entries.Add(new OutlineEntry
                {
                    Level = heading.Level,
                    Text = GetInlineText(heading.Inline),
                    Id = heading.GetAttributes().Id,
                });
            }

            return entries;
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
