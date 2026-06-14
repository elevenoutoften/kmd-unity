using System.Collections.Generic;
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

        // ColorCode's built-in dark/light dictionaries use a washed-out VS palette.
        // kmd highlights with Shiki's github-dark-default / github-light-default, so
        // map ColorCode's scopes onto those exact palettes for visual parity. The
        // seven colours below are GitHub's keyword / string / constant / comment /
        // entity-name (orange) / function (purple) / tag (green). Scopes left unmapped
        // (punctuation, operators, attribute names, plain text) inherit the code
        // block's foreground colour, which is what GitHub's themes do too.
        private static readonly StyleDictionary DarkStyles = BuildStyles(
            keyword: "#FFff7b72", str: "#FFa5d6ff", constant: "#FF79c0ff", comment: "#FF8b949e",
            entity: "#FFffa657", function: "#FFd2a8ff", tag: "#FF7ee787");

        private static readonly StyleDictionary LightStyles = BuildStyles(
            keyword: "#FFcf222e", str: "#FF0a3069", constant: "#FF0550ae", comment: "#FF6e7781",
            entity: "#FF953800", function: "#FF8250df", tag: "#FF116329");

        private readonly StringBuilder _buffer = new StringBuilder();

        public ColorCodeRichTextFormatter()
            : this(EditorGUIUtility.isProSkin)
        {
        }

        public ColorCodeRichTextFormatter(bool dark)
            : base(dark ? DarkStyles : LightStyles, SharedParser)
        {
        }

        // Builds a ColorCode StyleDictionary that buckets every common scope into one
        // of the seven GitHub-theme colours. Foregrounds are ARGB ("#FFRRGGBB"); the
        // formatter strips the alpha when emitting UIToolkit <color> tags.
        private static StyleDictionary BuildStyles(
            string keyword, string str, string constant, string comment,
            string entity, string function, string tag)
        {
            var styles = new StyleDictionary();

            void Add(string scope, string color) => styles.Add(new Style(scope) { Foreground = color });

            // Keywords + operators that read as keywords (GitHub: keyword/storage red).
            Add(ScopeName.Keyword, keyword);
            Add(ScopeName.ControlKeyword, keyword);
            Add(ScopeName.PseudoKeyword, keyword);
            Add(ScopeName.PreprocessorKeyword, keyword);
            Add(ScopeName.HtmlOperator, keyword);
            Add(ScopeName.PowerShellOperator, keyword);
            Add(ScopeName.MarkdownListItem, keyword);

            // Strings and string-like values (GitHub: string light-blue).
            Add(ScopeName.String, str);
            Add(ScopeName.StringCSharpVerbatim, str);
            Add(ScopeName.JsonString, str);
            Add(ScopeName.MarkdownCode, str);
            Add(ScopeName.HtmlAttributeValue, str);
            Add(ScopeName.XmlAttributeValue, str);
            Add(ScopeName.XmlAttributeQuotes, str);
            Add(ScopeName.XmlCDataSection, str);
            Add(ScopeName.CssPropertyValue, str);

            // Numbers, constants, support/builtin types, escapes (GitHub: constant blue).
            Add(ScopeName.Number, constant);
            Add(ScopeName.JsonNumber, constant);
            Add(ScopeName.JsonConst, constant);
            Add(ScopeName.BuiltinValue, constant);
            Add(ScopeName.Predefined, constant);
            Add(ScopeName.Intrinsic, constant);
            Add(ScopeName.StringEscape, constant);
            Add(ScopeName.SpecialCharacter, constant);
            Add(ScopeName.HtmlEntity, constant);
            Add(ScopeName.CssPropertyName, constant);
            Add(ScopeName.PowerShellType, constant);
            Add(ScopeName.MarkdownHeader, constant);

            // Comments (GitHub: grey).
            Add(ScopeName.Comment, comment);
            Add(ScopeName.HtmlComment, comment);
            Add(ScopeName.XmlComment, comment);
            Add(ScopeName.XmlDocComment, comment);
            Add(ScopeName.XmlDocTag, comment);

            // User-defined type/class names + variables (GitHub: entity.name/variable orange).
            Add(ScopeName.ClassName, entity);
            Add(ScopeName.Type, entity);
            Add(ScopeName.TypeVariable, entity);
            Add(ScopeName.PowerShellVariable, entity);
            Add(ScopeName.PowerShellParameter, entity);

            // Functions, constructors, attributes (GitHub: entity.name.function purple).
            Add(ScopeName.BuiltinFunction, function);
            Add(ScopeName.Constructor, function);
            Add(ScopeName.Attribute, function);
            Add(ScopeName.PowerShellCommand, function);
            Add(ScopeName.PowerShellAttribute, function);
            Add(ScopeName.SqlSystemFunction, function);

            // Markup element/tag names + JSON keys (GitHub: entity.name.tag green).
            Add(ScopeName.HtmlElementName, tag);
            Add(ScopeName.XmlName, tag);
            Add(ScopeName.CssSelector, tag);
            Add(ScopeName.JsonKey, tag);

            // Left unmapped (inherit foreground, matching GitHub): HtmlAttributeName,
            // XmlAttribute (attribute names), Operator, Delimiter, NameSpace, plain text.
            return styles;
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
            return UIMarkdownRenderer.EscapeRichText(text);
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
