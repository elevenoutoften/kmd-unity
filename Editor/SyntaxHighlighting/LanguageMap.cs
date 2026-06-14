using System;
using System.Collections.Generic;
using ColorCode;

namespace Kmd.MarkdownReader
{
    /// <summary>
    /// Resolves a Markdown fenced-code info string (e.g. "csharp", "py", "ts") to a
    /// ColorCode <see cref="ILanguage"/>. Returns null for anything unknown so the
    /// caller can fall back to plain, unhighlighted monospace.
    /// </summary>
    public static class LanguageMap
    {
        // Common Markdown fence aliases mapped to ColorCode language ids. ColorCode's
        // FindById already understands its own ids and a few aliases; this table only
        // covers the Markdown-world spellings it does not (cs, py, ts, c++, ps1, ...).
        private static readonly Dictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "cs", "c#" },
                { "csharp", "c#" },
                { "fsharp", "f#" },
                { "fs", "f#" },
                { "vb", "vb.net" },
                { "vbnet", "vb.net" },
                { "py", "python" },
                { "ts", "typescript" },
                { "tsx", "typescript" },
                { "js", "javascript" },
                { "mjs", "javascript" },
                { "cjs", "javascript" },
                { "node", "javascript" },
                { "jsx", "javascript" },
                { "c", "cpp" },
                { "c++", "cpp" },
                { "cc", "cpp" },
                { "cxx", "cpp" },
                { "h", "cpp" },
                { "hpp", "cpp" },
                { "ps", "powershell" },
                { "ps1", "powershell" },
                { "pwsh", "powershell" },
                { "jsonc", "json" },
                { "xaml", "xml" },
                { "csproj", "xml" },
                { "svg", "xml" },
                { "htm", "html" },
                { "xhtml", "html" },
                { "hs", "haskell" },
                { "md", "markdown" },
            };

        private static readonly char[] InfoSeparators = { ' ', '\t', ',' };

        /// <summary>
        /// Returns the ColorCode language for the given fence info string, or null if
        /// the language is empty or unrecognized.
        /// </summary>
        public static ILanguage Resolve(string fenceInfo)
        {
            if (string.IsNullOrWhiteSpace(fenceInfo))
            {
                return null;
            }

            // A fence info string can carry attributes after the id ("csharp title=x");
            // only the first token is the language.
            var token = fenceInfo.Trim();
            var separator = token.IndexOfAny(InfoSeparators);
            if (separator >= 0)
            {
                token = token.Substring(0, separator);
            }

            if (token.Length == 0)
            {
                return null;
            }

            var id = Aliases.TryGetValue(token, out var mapped) ? mapped : token;
            return Languages.FindById(id);
        }
    }
}
