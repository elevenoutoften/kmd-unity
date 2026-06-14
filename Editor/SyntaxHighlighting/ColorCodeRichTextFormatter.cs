using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using ColorCode;
using ColorCode.Common;
using ColorCode.Compilation;
using ColorCode.Parsing;
using ColorCode.Styling;
using UnityEditor;

namespace Kmd.MarkdownReader
{
    /// <summary>
    /// A ColorCode formatter that emits UIToolkit rich text (&lt;color=#RRGGBB&gt;...)
    /// instead of HTML. Token colors come from ColorCode's built-in dark/light style
    /// dictionaries, picked to match the current editor skin. Falls back to plain,
    /// escaped text when no language is supplied.
    /// </summary>
    public class ColorCodeRichTextFormatter : CodeColorizerBase
    {
        // The parser compiles language grammars on demand and caches them; it is
        // stateless across calls, so one shared instance serves every formatter.
        private static readonly ILanguageParser SharedParser = BuildParser();

        private readonly StringBuilder _buffer = new StringBuilder();

        public ColorCodeRichTextFormatter()
            : this(EditorGUIUtility.isProSkin)
        {
        }

        public ColorCodeRichTextFormatter(bool dark)
            : base(dark ? StyleDictionary.DefaultDark : StyleDictionary.DefaultLight, SharedParser)
        {
        }

        /// <summary>
        /// Highlights <paramref name="sourceCode"/> for <paramref name="language"/> and
        /// returns UIToolkit rich text. When <paramref name="language"/> is null the
        /// text is returned escaped but unhighlighted (the plain-monospace fallback).
        /// </summary>
        public string GetRichText(string sourceCode, ILanguage language)
        {
            _buffer.Clear();

            if (string.IsNullOrEmpty(sourceCode))
            {
                return string.Empty;
            }

            if (language == null)
            {
                _buffer.Append(Escape(sourceCode));
                return _buffer.ToString();
            }

            languageParser.Parse(sourceCode, language, Write);
            return _buffer.ToString();
        }

        // Invoked once per source segment by the parser (scoped tokens and the plain
        // gaps between them); appends rich text to the shared buffer.
        protected override void Write(string parsedSourceCode, IList<Scope> scopes)
        {
            WriteScopes(parsedSourceCode, scopes, 0, parsedSourceCode.Length);
        }

        // Emits text for [start, end): each scope's span is wrapped in a <color> tag
        // (recursing into nested scopes), and the gaps between scopes are escaped
        // plain text. Scopes are non-overlapping and ordered by index.
        private void WriteScopes(string source, IList<Scope> scopes, int start, int end)
        {
            var offset = start;

            foreach (var scope in scopes)
            {
                if (scope.Index > offset)
                {
                    _buffer.Append(Escape(source.Substring(offset, scope.Index - offset)));
                }

                var color = GetForeground(scope.Name);
                if (color != null)
                {
                    _buffer.Append("<color=").Append(color).Append('>');
                }

                WriteScopes(source, scope.Children, scope.Index, scope.Index + scope.Length);

                if (color != null)
                {
                    _buffer.Append("</color>");
                }

                offset = scope.Index + scope.Length;
            }

            if (end > offset)
            {
                _buffer.Append(Escape(source.Substring(offset, end - offset)));
            }
        }

        private string GetForeground(string scopeName)
        {
            if (Styles != null && Styles.Contains(scopeName))
            {
                return ToRichTextColor(Styles[scopeName].Foreground);
            }

            return null;
        }

        // ColorCode stores colors as #AARRGGBB; UIToolkit rich text wants #RRGGBB.
        private static string ToRichTextColor(string color)
        {
            if (string.IsNullOrEmpty(color) || color[0] != '#')
            {
                return null;
            }

            if (color.Length == 9) // #AARRGGBB -> drop the leading alpha byte
            {
                return "#" + color.Substring(3);
            }

            if (color.Length == 7) // already #RRGGBB
            {
                return color;
            }

            return null;
        }

        // Neutralize characters UIToolkit would otherwise parse as rich-text markup.
        // Matches the escaping used elsewhere in the renderer (LiteralInlineRenderer).
        private static string Escape(string text)
        {
            return WebUtility.HtmlEncode(text);
        }

        private static ILanguageParser BuildParser()
        {
            var loaded = new Dictionary<string, ILanguage>();
            foreach (var language in Languages.All)
            {
                if (!string.IsNullOrEmpty(language.Id) && !loaded.ContainsKey(language.Id))
                {
                    loaded[language.Id] = language;
                }
            }

            var repository = new LanguageRepository(loaded);
            var compiler = new LanguageCompiler(
                new Dictionary<string, CompiledLanguage>(),
                new ReaderWriterLockSlim());

            return new LanguageParser(compiler, repository);
        }
    }
}
