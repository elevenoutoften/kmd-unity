using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    public class UIMarkdownRenderer : RendererBase
    {
        private const float HeadingScrollPadding = 12f;

        public ScrollView RootElement { get; }
        public VisualElement ContentElement { get; }

        internal bool RichTextLinksClickable { get; }

        /// <summary>Directory used to resolve relative image/link paths.</summary>
        public string BaseDirectory { get; set; }

        /// <summary>The most recently parsed document (for outline extraction).</summary>
        public Markdig.Syntax.MarkdownDocument Document { get; private set; }

        private readonly Stack<VisualElement> _blockStack = new Stack<VisualElement>();

        private Label _currentLabel;
        private readonly StringBuilder _currentText = new StringBuilder();

        private readonly Dictionary<string, VisualElement> _headingRegistry = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, VisualElement> _outlineHeadingRegistry = new Dictionary<string, VisualElement>();
        private int _headingIndex;

        // Built once: the pipeline is immutable and rebuilding ~12 extensions per renderer is wasteful.
        private static readonly MarkdownPipeline SharedPipeline = CreatePipeline();

        public UIMarkdownRenderer()
        {
            RootElement = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "md-scroll-view",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
            };

            ContentElement = new VisualElement { name = "md-body" };
            RootElement.Add(ContentElement);

            // Best-effort link-click handling; false if Unity's internal link-tag
            // events aren't reachable (see LinkActivation). RootElement persists across
            // renders, so a single registration catches every link via bubbling.
            RichTextLinksClickable = LinkActivation.TryRegister(RootElement, this);

            ObjectRenderers.Add(new HeadingBlockRenderer());
            ObjectRenderers.Add(new ParagraphBlockRenderer());
            ObjectRenderers.Add(new LiteralInlineRenderer());
            ObjectRenderers.Add(new EmphasisInlineRenderer());
            ObjectRenderers.Add(new CodeInlineRenderer());
            ObjectRenderers.Add(new LineBreakInlineRenderer());
            ObjectRenderers.Add(new MathInlineRenderer());
            ObjectRenderers.Add(new TaskListInlineRenderer());
            ObjectRenderers.Add(new LinkInlineRenderer());
            ObjectRenderers.Add(new AutolinkInlineRenderer());
            ObjectRenderers.Add(new FootnoteLinkRenderer());
            ObjectRenderers.Add(new ThematicBreakBlockRenderer());
            ObjectRenderers.Add(new ListBlockRenderer());
            ObjectRenderers.Add(new AlertBlockRenderer()); // before QuoteBlockRenderer (AlertBlock : QuoteBlock)
            ObjectRenderers.Add(new QuoteBlockRenderer());
            ObjectRenderers.Add(new FencedCodeBlockRenderer());
            ObjectRenderers.Add(new CodeBlockRenderer());
            ObjectRenderers.Add(new TableBlockRenderer());
            ObjectRenderers.Add(new FootnoteGroupRenderer());
        }

        public static MarkdownPipeline CreatePipeline()
        {
            return new MarkdownPipelineBuilder()
                .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
                .UseAutoLinks()
                .UsePipeTables()
                .UseGridTables()
                .UseTaskLists()
                .UseEmphasisExtras() // Default options include Strikethrough (~~text~~)
                .UseFootnotes()
                .UseYamlFrontMatter()
                .UseGenericAttributes()
                .UseAlertBlocks()
                .UseMathematics()
                .Build();
        }

        public VisualElement Render(string markdown)
        {
            ContentElement.Clear();
            _headingRegistry.Clear();
            _outlineHeadingRegistry.Clear();
            _headingIndex = 0;
            _blockStack.Clear();
            _currentLabel = null;
            _currentText.Clear();
            Document = null;

            if (string.IsNullOrWhiteSpace(markdown))
            {
                var emptyLabel = new Label("This file is empty.")
                {
                    name = "md-empty",
                };
                emptyLabel.AddToClassList("md-empty");
                ContentElement.Add(emptyLabel);
                return RootElement;
            }

            try
            {
                var document = Markdown.Parse(markdown, SharedPipeline);
                Document = document;
                _blockStack.Push(ContentElement);
                Render(document);
                FlushText();
                _blockStack.Clear();
            }
            catch (Exception ex)
            {
                // Log the full exception (with stack trace) to the console for
                // developers, but keep the trace out of the rendered document UI.
                Debug.LogException(ex);
                ContentElement.Clear();
                _blockStack.Clear();
                var errorLabel = new Label("Error rendering markdown: " + ex.Message)
                {
                    name = "md-error",
                };
                errorLabel.AddToClassList("md-error");
                ContentElement.Add(errorLabel);
            }

            return RootElement;
        }

        internal static void MakeLabelSelectable(Label label)
        {
            if (label == null || !label.enableRichText)
            {
                return;
            }

            if (label.ClassListContains("md-code-inline")
                || label.ClassListContains("md-inline-link")
                || label.ClassListContains("md-codeblock-text")
                || label.ClassListContains("md-error")
                || label.ClassListContains("md-empty"))
            {
                return;
            }

            var selection = label.selection;
            selection.isSelectable = true;
            selection.selectionColor = new Color(0.608f, 0.427f, 1f, 0.3f);
        }

        // USS has no :last-child selector, so a stylesheet can't strip the trailing
        // bottom margin of the last child inside a container. Without it, blockquotes
        // and alerts gain a doubled gap at the bottom (inner padding + the last
        // paragraph's margin). Call this after writing a container's children to
        // match kmd's `blockquote/alert p:last-child { margin-bottom: 0 }`.
        public static void TrimTrailingMargin(VisualElement container)
        {
            if (container != null && container.childCount > 0)
            {
                container.ElementAt(container.childCount - 1).style.marginBottom = 0f;
            }
        }

        public VisualElement RenderFile(string path)
        {
            if (!File.Exists(path))
            {
                ContentElement.Clear();
                var errorLabel = new Label("File not found: " + path)
                {
                    name = "md-error",
                };
                errorLabel.AddToClassList("md-error");
                ContentElement.Add(errorLabel);
                return RootElement;
            }

            BaseDirectory = Path.GetDirectoryName(Path.GetFullPath(path));

            string markdown;
            try
            {
                markdown = File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // A save race (file mid-write or briefly locked) must not throw out of
                // an editor callback; keep whatever was last rendered.
                Debug.LogException(ex);
                return RootElement;
            }

            return Render(markdown);
        }

        public void StartBlock(VisualElement element)
        {
            FlushText();
            AddToCurrentBlock(element);
            _blockStack.Push(element);
        }

        public void FinishBlock()
        {
            FlushText();
            if (_blockStack.Count > 1)
            {
                _blockStack.Pop();
            }
        }

        public void StartNewText()
        {
            FlushText();
        }

        public Label StartTextElement(string name)
        {
            FlushText();
            var label = new Label
            {
                name = name,
                enableRichText = true,
            };
            MakeLabelSelectable(label);
            AddToCurrentBlock(label);
            _currentLabel = label;
            _currentText.Clear();
            return label;
        }

        public void WriteText(string text)
        {
            if (_currentLabel == null)
            {
                _currentLabel = new Label { enableRichText = true };
                MakeLabelSelectable(_currentLabel);
                AddToCurrentBlock(_currentLabel);
                _currentText.Clear();
            }

            _currentText.Append(text);
        }

        public void FlushText()
        {
            if (_currentLabel != null && _currentText.Length > 0)
            {
                _currentLabel.text = _currentText.ToString();
            }

            _currentLabel = null;
            _currentText.Clear();
        }

        public void AddToCurrentBlock(VisualElement element)
        {
            if (_blockStack.Count > 0)
            {
                _blockStack.Peek().Add(element);
            }
            else
            {
                ContentElement.Add(element);
            }
        }

        public void RegisterOutlineHeading(string id, VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            _outlineHeadingRegistry[OutlineExtractor.CreateAnchor(_headingIndex++)] = element;
            RegisterHeading(id, element);
        }

        public void RegisterHeading(string id, VisualElement element)
        {
            if (!string.IsNullOrEmpty(id) && !_headingRegistry.ContainsKey(id))
            {
                _headingRegistry[id] = element;
            }
        }

        public bool TryGetHeading(string id, out VisualElement element)
        {
            return _headingRegistry.TryGetValue(id, out element);
        }

        public bool TryGetOutlineHeading(string anchor, out VisualElement element)
        {
            return _outlineHeadingRegistry.TryGetValue(anchor, out element);
        }

        public ScrollView ScrollToOutlineHeading(string anchor)
        {
            VisualElement element;
            if (_outlineHeadingRegistry.TryGetValue(anchor, out element))
            {
                ScrollToElementTop(element);
            }

            return RootElement;
        }

        public ScrollView ScrollToHeading(string id)
        {
            VisualElement element;
            if (_headingRegistry.TryGetValue(id, out element))
            {
                ScrollToElementTop(element);
            }

            return RootElement;
        }

        private void ScrollToElementTop(VisualElement element)
        {
            if (!TryScrollToElementTop(element))
            {
                RootElement.ScrollTo(element);
                RootElement.schedule.Execute(() => { TryScrollToElementTop(element); });
            }
        }

        private bool TryScrollToElementTop(VisualElement element)
        {
            if (element == null
                || RootElement.panel == null
                || element.panel == null
                || RootElement.layout.height <= 0f
                || RootElement.contentContainer.layout.height <= 0f)
            {
                return false;
            }

            var currentOffset = RootElement.scrollOffset;
            var targetOffset = currentOffset.y
                + element.worldBound.yMin
                - RootElement.worldBound.yMin
                - HeadingScrollPadding;

            if (float.IsNaN(targetOffset) || float.IsInfinity(targetOffset))
            {
                return false;
            }

            var maxOffset = Mathf.Max(
                0f,
                RootElement.contentContainer.layout.height - RootElement.layout.height);
            RootElement.scrollOffset = new Vector2(
                currentOffset.x,
                Mathf.Clamp(targetOffset, 0f, maxOffset));
            return true;
        }

        // UI Toolkit's rich-text parser treats only '<' as markup and decodes NO
        // HTML entities — &lt;, &#39;, &amp; all render literally. So neutralize
        // just '<' with a trailing zero-width space (it no longer starts a tag but
        // still shows as '<'); every other character (incl. & > ' ") is left as-is.
        public static string EscapeRichText(string text)
        {
            // The overwhelmingly common run has no '<' at all — skip the allocating
            // Replace entirely in that case.
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
            {
                return text;
            }

            return text.Replace("<", "<​");
        }

        // Append a slice of text to a buffer with the same '<' neutralization as
        // EscapeRichText, without allocating an intermediate Substring + Replace per
        // run (hot for large syntax-highlighted code blocks).
        public static void AppendEscaped(StringBuilder builder, string text, int start, int length)
        {
            if (builder == null || string.IsNullOrEmpty(text) || length <= 0)
            {
                return;
            }

            var end = start + length;
            for (var i = start; i < end; i++)
            {
                var c = text[i];
                builder.Append(c);
                if (c == '<')
                {
                    builder.Append('​');
                }
            }
        }

        public override object Render(Markdig.Syntax.MarkdownObject markdownObject)
        {
            Write(markdownObject);
            return this;
        }
    }
}
