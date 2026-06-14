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

            // kmd renders alert titles uppercase (CSS text-transform); UI Toolkit
            // has no text-transform, so use the already-uppercased kind directly.
            var title = new Label(kind) { name = "md-alert-title" };
            title.AddToClassList("md-alert-title");
            title.AddToClassList("md-alert-title-" + suffix);
            renderer.AddToCurrentBlock(title);

            renderer.WriteChildren(alert);

            renderer.FinishBlock();
        }
    }
}
