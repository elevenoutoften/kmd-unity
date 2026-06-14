# kmd-unity Architecture

Native Unity UIToolkit rendering of Markdown, porting kmd's reader mode (not design mode) to the Unity Editor.

## Approach

Rebuild kmd's reader features natively in C# using Markdig + UIToolkit. No WebView, no embedded browser, no external process dependency.

**Why not embed a webview?** Unity has no public API for embedding webviews in editor panels. CEF was removed in Unity 2020.1. Reflection-based internal WebView hacks break when docked and across Unity versions. The only reliable path is native UIToolkit rendering.

**Why not wrap MarkdownRenderer?** [MarkdownRenderer](https://github.com/UnityGuillaume/MarkdownRenderer) is the best existing Unity package, but it only covers basic Markdown (no tables, no syntax highlighting, no copy button, no alerts, no task lists). We need kmd-quality rendering, which requires a richer pipeline and more renderers. Forking would mean rewriting most renderers anyway, so we build our own on the same Markdig foundation.

## Pipeline

```
.md file → Markdig pipeline → AST → UIToolkit VisualElement tree → USS styling
```

### Markdig Pipeline

```csharp
var pipeline = new MarkdownPipelineBuilder()
    .UseAutoIdentifiers()      // heading slugs for anchor links
    .UseAutoLinks()             // bare URLs become links
    .UsePipeTables()             // GFM pipe tables
    .UseGridTables()             // grid tables
    .UseTaskLists()              // - [ ] / - [x] checkboxes
    .UseStrikethrough()          // ~~strikethrough~~
    .UseEmphasisExtras()         // subscript, superscript, inserted, marked
    .UseFootnotes()              // footnote syntax
    .UseYamlFrontMatter()        // parse YAML front matter
    .UseGenericAttributes()      // {.class} for custom styling
    .UseAlertBlocks()            // GitHub-style > [!NOTE] alerts
    .UseMath()                   // $$...$$ and $...$ math (fallback rendering)
    .Build();
```

### Rendering: Markdig AST → UIToolkit

Each Markdig AST node type maps to a custom `MarkdownObjectRenderer` that produces `VisualElement` nodes:

| Markdig Node | Renderer | UIToolkit Output |
|---|---|---|
| `HeadingBlock` | `HeadingBlockRenderer` | Label with `md-h1`–`md-h6` class, anchor registration |
| `ParagraphBlock` | `ParagraphBlockRenderer` | Label with `md-paragraph` class |
| `ListBlock` | `ListBlockRenderer` | VisualElement with `md-list`, `<margin-left>` rich text for bullets |
| `FencedCodeBlock` | `FencedCodeBlockRenderer` | VisualElement `md-codeblock` + Label (syntax-highlighted) + copy button |
| `CodeBlock` | `CodeBlockRenderer` | Label with `md-code` (indented code, no highlighting) |
| `QuoteBlock` | `QuoteBlockRenderer` | VisualElement with `md-blockquote` + left border accent |
| `ThematicBreakBlock` | `ThematicBreakRenderer` | VisualElement with `md-hr` |
| `TableBlock` | `TableBlockRenderer` | VisualElement grid inside ScrollView |
| `AlertBlock` | `AlertBlockRenderer` | Styled callout with icon + title + body |
| `EmphasisInline` | `EmphasisInlineRenderer` | `<b>` / `<i>` / `<s>` rich text tags |
| `CodeInline` | `CodeInlineRenderer` | Inline label with `md-code-inline` class |
| `LinkInline` | `LinkInlineRenderer` | `<link="url">` tags with click callbacks |
| `LiteralInline` | `LiteralInlineRenderer` | Plain text |
| `FootnoteLink` | `FootnoteLinkRenderer` | Superscript link |
| `MathInline` / `MathBlock` | `MathRenderer` | Styled placeholder (v1) |

## Syntax Highlighting

ColorCode.Core (MIT, .NET Standard 1.4) tokenizes source code into colored spans. A custom `RichTextFormatter` converts tokens to UIToolkit `<color=#hex>` rich text tags:

```
<color=#569CD6>using<color=#FFFFFF> <color=#4EC9B0>UnityEngine<color=#FFFFFF>;
```

Each code block renders as a single `Label` with `enableRichText = true` and a monospace font. Two theme palettes (light/dark) match the editor skin.

## Feature Parity with kmd Reader

| kmd Feature | Status | Notes |
|---|---|---|
| Headings (h1–h6) | ✅ v1 | With anchor IDs and scroll-to |
| Paragraphs | ✅ v1 | |
| Bold / Italic / Strikethrough | ✅ v1 | `<b>`, `<i>`, `<s>` rich text tags |
| Links (fragment, internal, external) | ✅ v1 | `<link>` tags, URL policy for security |
| Inline code | ✅ v1 | Click-to-copy via `GUIUtility.systemCopyBuffer` |
| Fenced code blocks | ✅ v1 | Syntax highlighting + copy button |
| Indented code blocks | ✅ v1 | Plain monospace |
| GFM tables | ✅ v1 | Grid layout in ScrollView |
| Task lists | ✅ v1 | Read-only `Toggle` checkboxes |
| Blockquotes | ✅ v1 | Left border accent |
| Ordered / unordered lists | ✅ v1 | Nested via `<margin-left>` |
| Thematic breaks | ✅ v1 | Horizontal rule |
| GitHub Alerts | ✅ v1 | NOTE/TIP/IMPORTANT/WARNING/CAUTION callouts |
| Footnotes | ✅ v1 | Superscript links + footnote section |
| Autolinks | ✅ v1 | Bare URLs become clickable |
| Emphasis extras | ✅ v1 | `~~strike~~`, `^sup^`, `~sub~`, `++insert++`, `==mark==` |
| Outline sidebar | ✅ v1 | Heading tree + scroll spy |
| Dark / light theme | ✅ v1 | Two USS files, auto-detect editor skin |
| Image loading | ✅ v1 | `UnityWebRequestTexture`, relative path resolution |
| YAML front matter | ✅ v1 | Parsed, not rendered (available for custom styling) |
| Mermaid diagrams | ⏳ v2 | Placeholder in v1. Native rendering not feasible |
| KaTeX math | ⏳ v2 | Placeholder in v1. No C# KaTeX equivalent |
| Two-phase rendering | ❌ N/A | Not needed — C# Markdig is synchronous and fast |

## Design Tokens

Ported from kmd's `DESIGN.md`:

| Token | Value |
|---|---|
| Font body | Inter (3 weights: Regular, SemiBold, Bold) |
| Font mono | JetBrains Mono (2 weights: Regular, Bold) |
| Content max-width | 800px |
| Line-height body | `<line-height=1.62>` rich text tag (USS has no `line-height`) |
| Letter-spacing labels | `0.08em` |
| Border-radius sm/md/lg/xl | 6 / 8 / 12 / 16px |
| Spacing xs/sm/md/lg/xl/xxl | 4 / 8 / 16 / 24 / 36 / 56px |
| Colors (dark) | primary: #E1E4E8, secondary: #8B949E, surface: #0D1117 |
| Colors (light) | primary: #1F2328, secondary: #656D76, surface: #FFFFFF |

## Package Structure

```
love.axis.kmd-unity/
├── package.json
├── README.md
├── CHANGELOG.md
├── LICENSE.md
├── Editor/
│   ├── Kmd.MarkdownReader.Editor.asmdef
│   ├── MarkdownReader/
│   │   ├── UIMarkdownRenderer.cs          # Main entry: pipeline → VisualElement tree
│   │   ├── MarkdownInspector.cs           # CustomEditor for .md assets
│   │   ├── MarkdownViewer.cs              # EditorWindow for standalone viewing
│   │   └── DocumentShell.cs               # Outline sidebar + scroll container
│   ├── ObjectRenderers/
│   │   ├── HeadingBlockRenderer.cs
│   │   ├── ParagraphBlockRenderer.cs
│   │   ├── ListBlockRenderer.cs
│   │   ├── FencedCodeBlockRenderer.cs
│   │   ├── CodeBlockRenderer.cs
│   │   ├── TableBlockRenderer.cs
│   │   ├── QuoteBlockRenderer.cs
│   │   ├── AlertBlockRenderer.cs
│   │   ├── ThematicBreakRenderer.cs
│   │   ├── EmphasisInlineRenderer.cs
│   │   ├── CodeInlineRenderer.cs
│   │   ├── LinkInlineRenderer.cs
│   │   ├── LiteralInlineRenderer.cs
│   │   ├── LineBreakInlineRenderer.cs
│   │   ├── FootnoteRenderer.cs
│   │   └── MathRenderer.cs               # Placeholder for v1
│   ├── SyntaxHighlighting/
│   │   ├── ColorCodeRichTextFormatter.cs  # ColorCode → <color> tags
│   │   └── LanguageMap.cs                 # Markdown lang ID → ColorCode language
│   ├── Utilities/
│   │   ├── UrlPolicy.cs                  # URL validation (port from kmd sanitize.ts)
│   │   ├── ImageLoader.cs                # Async image loading via UnityWebRequest
│   │   └── ThemeManager.cs               # Dark/light USS switching
│   └── Styles/
│       ├── MarkdownReaderLight.uss
│       └── MarkdownReaderDark.uss
├── Plugins/
│   ├── Markdig.dll                        # .NET Standard 2.0
│   └── ColorCode.Core.dll                 # .NET Standard 1.4
├── Fonts/
│   ├── Inter-Regular.ttf
│   ├── Inter-SemiBold.ttf
│   ├── Inter-Bold.ttf
│   ├── JetBrainsMono-Regular.ttf
│   └── JetBrainsMono-Bold.ttf
└── Tests/
    └── Editor/
        └── Kmd.MarkdownReader.Editor.Tests.asmdef
```

## UIToolkit Constraints

| CSS Feature | UIToolkit Support | Workaround |
|---|---|---|
| `line-height` | ❌ No USS property | `<line-height=N>` rich text tag inside Labels |
| Variable fonts | ❌ Not supported | Ship separate .ttf per weight |
| Custom selection colors | ❌ Editor theme controls | Accept |
| Multi-line text ellipsis | ❌ Known bug | Accept for v1 |
| `text-shadow` | ⚠️ Limited (~5px) | Avoid for body text |
| Ligatures | ❌ Not supported | Accept (mono fonts rarely need them) |
| `<color=#hex>` in Labels | ✅ Full support | Primary mechanism for syntax highlighting |
| `<link="url">` in Labels | ✅ Unity 2022.2+ | Minimum target version |
| `letter-spacing` | ✅ Full support | |
| Custom fonts (.ttf/.otf) | ✅ Bundle in package | Reference via USS `resource()` |

## Minimum Unity Version

**2022.3 LTS** — Required for native `<link>` tag support in UIToolkit Labels (`PointerClickLinkTagEvent`).

## Dependencies

- **Markdig** (BSD-2-Clause) — Markdown parsing. Shipped as compiled DLL targeting .NET Standard 2.0.
- **ColorCode.Core** (MIT) — Syntax highlighting. Shipped as compiled DLL targeting .NET Standard 1.4.
- **Inter** (SIL OFL) — Body font. Shipped as .ttf files.
- **JetBrains Mono** (SIL OFL) — Code font. Shipped as .ttf files.

All dependencies are permissively licensed and can be bundled in the package.

## Click-to-Copy

```csharp
// Code block copy button
var copyButton = new Button { text = "📋" };
copyButton.clicked += () => {
    GUIUtility.systemCopyBuffer = codeText;
    copyButton.text = "✓";
    schedule.Execute(() => copyButton.text = "📋").ExecuteLater(1500);
};

// Inline code click-to-copy
codeLabel.RegisterCallback<ClickEvent>(evt => {
    GUIUtility.systemCopyBuffer = codeLabel.text;
});
```

## Inspector Integration

```csharp
[CustomEditor(typeof(TextAsset))]
public class MarkdownInspector : Editor {
    private UIMarkdownRenderer _renderer;

    void OnEnable() {
        var path = AssetDatabase.GetAssetPath(target);
        if (path.EndsWith(".md")) {
            _renderer = new UIMarkdownRenderer();
            _renderer.LoadFile(path);
        }
    }

    public override VisualElement CreateInspectorGUI() {
        return _renderer?.RootElement ?? base.CreateInspectorGUI();
    }
}
```

Also supports `Selection.selectionChanged` for auto-loading `.md` files in a standalone `MarkdownViewer` EditorWindow.

## Security

Port kmd's URL policy from `sanitize.ts`:

- Block `javascript:`, `vbscript:`, unsafe `data:`, arbitrary `file:`, unknown custom schemes
- External links open via `Application.OpenURL()` (OS browser), never inside the reader
- Relative image paths resolved via `UnityWebRequest` against the document's directory
- No `<script>`, no event handlers, no raw HTML rendering (Markdig outputs structured AST, not HTML)