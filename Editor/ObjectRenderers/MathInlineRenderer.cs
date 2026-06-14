using Markdig.Extensions.Mathematics;
using Markdig.Renderers;

namespace Kmd.MarkdownReader
{
    // $...$ inline math. There is no math engine here, so render the raw LaTeX
    // inline rather than dropping it (real KaTeX-style rendering is out of scope).
    // MathBlock ($$...$$ / ```math) derives from CodeBlock and is already handled
    // by CodeBlockRenderer as a raw code box.
    public class MathInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, MathInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, MathInline obj)
        {
            renderer.WriteText(UIMarkdownRenderer.EscapeRichText(obj.Content.ToString()));
        }
    }
}
