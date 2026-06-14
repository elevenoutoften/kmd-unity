using Markdig.Extensions.Alerts;
using Markdig.Renderers;
using UnityEngine.UIElements;

namespace Kmd.MarkdownReader
{
    // GitHub-style alert callouts (> [!NOTE] etc.). AlertBlock derives from
    // QuoteBlock, so this renderer must be registered BEFORE QuoteBlockRenderer.
    public class AlertBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, AlertBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, AlertBlock alert)
        {
            renderer.FlushText();

            var kind = alert.Kind.ToString();
            kind = string.IsNullOrEmpty(kind) ? "NOTE" : kind.Trim().ToUpperInvariant();
            var suffix = kind.ToLowerInvariant();

            var container = new VisualElement { name = "md-alert" };
            container.AddToClassList("md-alert");
            container.AddToClassList("md-alert-" + suffix);
            renderer.StartBlock(container);

            var title = new Label(TitleCase(kind)) { name = "md-alert-title" };
            title.AddToClassList("md-alert-title");
            title.AddToClassList("md-alert-title-" + suffix);
            renderer.AddToCurrentBlock(title);

            renderer.WriteChildren(alert);

            renderer.FinishBlock();
        }

        private static string TitleCase(string kind)
        {
            if (kind.Length == 0)
            {
                return kind;
            }

            return char.ToUpperInvariant(kind[0]) + kind.Substring(1).ToLowerInvariant();
        }
    }
}
