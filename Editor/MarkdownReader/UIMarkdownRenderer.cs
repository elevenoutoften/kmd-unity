using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    public class UIMarkdownRenderer : RendererBase
    {
        public ScrollView RootElement { get; }
        public VisualElement ContentElement { get; }

        /// <summary>Directory used to resolve relative image/link paths.</summary>
        public string BaseDirectory { get; set; }

        private readonly Stack<VisualElement> _blockStack = new Stack<VisualElement>();

        private Label _currentLabel;
        private readonly StringBuilder _currentText = new StringBuilder();

        private readonly Dictionary<string, VisualElement> _headingRegistry = new Dictionary<string, VisualElement>();

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
            ContentElement.RegisterCallback<PointerUpLinkTagEvent>(OnLinkClicked);

            ObjectRenderers.Add(new HeadingBlockRenderer());
            ObjectRenderers.Add(new ParagraphBlockRenderer());
            ObjectRenderers.Add(new LiteralInlineRenderer());
            ObjectRenderers.Add(new EmphasisInlineRenderer());
            ObjectRenderers.Add(new CodeInlineRenderer());
            ObjectRenderers.Add(new LineBreakInlineRenderer());
            ObjectRenderers.Add(new TaskListInlineRenderer());
            ObjectRenderers.Add(new LinkInlineRenderer());
            ObjectRenderers.Add(new AutolinkInlineRenderer());
            ObjectRenderers.Add(new ThematicBreakBlockRenderer());
            ObjectRenderers.Add(new ListBlockRenderer());
            ObjectRenderers.Add(new AlertBlockRenderer()); // before QuoteBlockRenderer (AlertBlock : QuoteBlock)
            ObjectRenderers.Add(new QuoteBlockRenderer());
            ObjectRenderers.Add(new FencedCodeBlockRenderer());
            ObjectRenderers.Add(new CodeBlockRenderer());
            ObjectRenderers.Add(new TableBlockRenderer());
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
            _blockStack.Clear();
            _currentLabel = null;
            _currentText.Clear();

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
                _blockStack.Push(ContentElement);
                Render(document);
                FlushText();
                _blockStack.Clear();
            }
            catch (Exception ex)
            {
                ContentElement.Clear();
                _blockStack.Clear();
                var errorLabel = new Label("Error rendering markdown:\n" + ex.Message)
                {
                    name = "md-error",
                };
                errorLabel.AddToClassList("md-error");
                ContentElement.Add(errorLabel);
            }

            return RootElement;
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
            var markdown = File.ReadAllText(path);
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

        public ScrollView ScrollToHeading(string id)
        {
            VisualElement element;
            if (_headingRegistry.TryGetValue(id, out element))
            {
                RootElement.ScrollTo(element);
            }

            return RootElement;
        }

        private void OnLinkClicked(PointerUpLinkTagEvent evt)
        {
            var url = evt.linkID;
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            switch (UrlPolicy.Classify(url))
            {
                case UrlKind.Fragment:
                    ScrollToHeading(url.TrimStart('#'));
                    break;
                case UrlKind.External:
                    UnityEngine.Application.OpenURL(url);
                    break;
                // Relative -> reserved for opening .md in the viewer (future); Blocked -> ignore.
            }
        }

        public override object Render(Markdig.Syntax.MarkdownObject markdownObject)
        {
            Write(markdownObject);
            return this;
        }
    }
}
