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
    .UseAutoIdentifiers(AutoIdentifierOptions.GitHub) // heading slugs for anchor links
    .UseAutoLinks()              // bare URLs become links
    .UsePipeTables()             // GFM pipe tables
    .UseGridTables()             // grid tables
    .UseTaskLists()              // - [ ] / - [x] checkboxes
    .UseEmphasisExtras()         // ~~strike~~, sub/superscript, inserted, marked
    .UseFootnotes()              // footnote syntax
    .UseYamlFrontMatter()        // parse YAML front matter
    .UseGenericAttributes()      // {.class} for custom styling
    .UseAlertBlocks()            // GitHub-style > [!NOTE] alerts
    .UseMathematics()            // $$...$$ and $...$ math (fallback rendering)
    .Build();
```

The pipeline is built once into a static, shared `MarkdownPipeline` and reused for
every render (it is immutable).

### Rendering: Markdig AST → UIToolkit

Each Markdig AST node type maps to a custom `MarkdownObjectRenderer` that produces `VisualElement` nodes:

| Markdig Node | Renderer | UIToolkit Output |
|---|---|---|
| `HeadingBlock` | `HeadingBlockRenderer` | Label with `md-h1`–`md-h6` class, anchor registration |
| `ParagraphBlock` | `ParagraphBlockRenderer` | Single rich-text Label, or `InlineFlowBuilder` flow when links/code present |
| `ListBlock` | `ListBlockRenderer` | VisualElement with `md-list`, `<margin-left>` rich text for bullets |
| `FencedCodeBlock` | `FencedCodeBlockRenderer` | VisualElement `md-codeblock` + Label (syntax-highlighted) + copy button |
| `CodeBlock` | `CodeBlockRenderer` | Label with `md-code` (indented code, no highlighting) |
| `QuoteBlock` | `QuoteBlockRenderer` | VisualElement with `md-blockquote` + left border accent |
| `ThematicBreakBlock` | `ThematicBreakBlockRenderer` | VisualElement with `md-hr` |
| `TableBlock` | `TableBlockRenderer` | VisualElement grid inside ScrollView; columns are measured and pinned once after first layout |
| `AlertBlock` | `AlertBlockRenderer` | Styled callout with icon + title + body |
| `EmphasisInline` | `EmphasisInlineRenderer` | `<b>` / `<i>` / `<s>` rich text tags |
| `CodeInline` | `CodeInlineRenderer` | Inline code chip (click-to-copy) via `InlineFlowBuilder.EmitChip` |
| `LinkInline` | `LinkInlineRenderer` | `<link="url">` rich text (best-effort click routing) |
| `AutolinkInline` | `AutolinkInlineRenderer` | `<link="url">` rich text for bare URLs/emails |
| `LiteralInline` | `LiteralInlineRenderer` | Plain text (`<`-neutralized for rich text) |
| `TaskList` | `TaskListInlineRenderer` | Inline ☑/☐ glyph |
| `FootnoteLink` | `FootnoteLinkRenderer` | Superscript `<link="#fn-N">` |
| `FootnoteGroup` | `FootnoteGroupRenderer` | Footnote section appended at document end |
| `MathInline` | `MathInlineRenderer` | Styled placeholder/fallback (v1) |

## Syntax Highlighting

ColorCode.Core (MIT, .NET Standard 1.4) tokenizes source code into colored spans. A custom `ColorCodeRichTextFormatter` converts tokens to UIToolkit `<color=#hex>` rich text tags:

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
| Links (fragment, internal, external) | ✅ v1 | `<link>` tags + sanitized URL policy; click-to-open is best-effort (see Security) |
| Inline code | ✅ v1 | Click-to-copy chip via `InlineFlowBuilder` |
| Fenced code blocks | ✅ v1 | Syntax highlighting + copy button |
| Indented code blocks | ✅ v1 | Plain monospace |
| GFM tables | ✅ v1 | Grid layout in ScrollView; widths do not reflow on window resize |
| Task lists | ✅ v1 | Read-only ☑/☐ glyphs |
| Blockquotes | ✅ v1 | Left border accent |
| Ordered / unordered lists | ✅ v1 | Nested via `<margin-left>` |
| Thematic breaks | ✅ v1 | Horizontal rule |
| GitHub Alerts | ✅ v1 | NOTE/TIP/IMPORTANT/WARNING/CAUTION callouts |
| Footnotes | ✅ v1 | Superscript links + footnote section |
| Autolinks | ✅ v1 | Bare URLs styled as links (best-effort click-to-open) |
| Emphasis extras | ⚠️ partial | `~~strike~~` → strikethrough; parsed but rendered generically: `^sup^`/`~sub~` → italic, `++insert++`/`==mark==` → bold |
| Outline sidebar | ✅ v1 | Heading tree + scroll spy |
| Dark / light theme | ✅ v1 | kmd (dark/light sheets) + Unity (skin-driven) themes |
| Image loading | ✅ v1 | Cached `UnityWebRequestTexture`; image policy (remote/out-of-project behind opt-in) |
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
│   │   ├── MarkdownViewerWindow.cs        # EditorWindow for standalone viewing
│   │   ├── DocumentShell.cs               # Outline sidebar + scroll container
│   │   ├── OutlineExtractor.cs            # Heading tree extraction for the outline
│   │   └── InlineFlowBuilder.cs           # Inline flow: links + click-to-copy code chips
│   ├── ObjectRenderers/
│   │   ├── HeadingBlockRenderer.cs
│   │   ├── ParagraphBlockRenderer.cs
│   │   ├── ListBlockRenderer.cs
│   │   ├── FencedCodeBlockRenderer.cs
│   │   ├── CodeBlockRenderer.cs
│   │   ├── TableBlockRenderer.cs
│   │   ├── QuoteBlockRenderer.cs
│   │   ├── AlertBlockRenderer.cs
│   │   ├── ThematicBreakBlockRenderer.cs
│   │   ├── EmphasisInlineRenderer.cs
│   │   ├── CodeInlineRenderer.cs
│   │   ├── LinkInlineRenderer.cs
│   │   ├── AutolinkInlineRenderer.cs
│   │   ├── LiteralInlineRenderer.cs
│   │   ├── LineBreakInlineRenderer.cs
│   │   ├── TaskListInlineRenderer.cs
│   │   ├── MathInlineRenderer.cs          # Styled placeholder/fallback for v1
│   │   ├── FootnoteLinkRenderer.cs
│   │   └── FootnoteGroupRenderer.cs
│   ├── SyntaxHighlighting/
│   │   ├── ColorCodeRichTextFormatter.cs  # ColorCode → <color> tags
│   │   └── LanguageMap.cs                 # Markdown lang ID → ColorCode language
│   ├── Utilities/
│   │   ├── UrlPolicy.cs                   # Link URL validation (port from kmd sanitize.ts)
│   │   ├── ImagePolicy.cs                 # Image-source policy (remote/external opt-in)
│   │   ├── ImageLoader.cs                 # Cached async image loading via UnityWebRequest
│   │   ├── LinkActivation.cs              # Link-target routing + best-effort click wiring
│   │   ├── CodeBlockCopyButton.cs         # Copy button for fenced code blocks
│   │   ├── ThemeManager.cs                # Theme/USS switching (kmd + Unity)
│   │   └── MarkdownReaderSettingsProvider.cs # Preferences ▸ Kmd Markdown
│   └── Styles/
│       ├── MarkdownReaderLight.uss
│       ├── MarkdownReaderDark.uss
│       └── MarkdownReaderUnity.uss
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

## Table Layout Behavior

Tables align columns with a one-shot measure-then-pin pass after the first layout.
This is intentional: UI Toolkit has no native table layout, so each row would
otherwise size its cells independently and columns would drift out of alignment.

Column widths do not reflow when the window is resized. Wide tables sit inside the
`md-table-scroll` horizontal `ScrollView`, which prevents content clipping without
adding per-resize layout work.

Theme or font changes still reset table widths because the document is fully
re-rendered by `MarkdownViewerWindow`, which rebuilds the table and repeats the
initial alignment pass.

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
| `<link="url">` rendering | ✅ Unity 2022.2+ | Renders/styles link runs |
| `<link>` click events | ⚠️ `internal` API | `Pointer*LinkTagEvent` is internal in 6000.3.x → click handled best-effort via reflection (`LinkActivation`) |
| `letter-spacing` | ✅ Full support | |
| Custom fonts (.ttf/.otf) | ✅ Bundle in package | Reference via USS `resource()` |

## Minimum Unity Version

**2022.3 LTS** — Required for `<link>` tag *rendering* in UIToolkit Labels. Note the
matching click events (`PointerUpLinkTagEvent` / `PointerClickLinkTagEvent`) are
`internal` in current Unity (6000.3.x), so `LinkActivation` wires click handling
through reflection on a best-effort basis and silently degrades to "styled but inert"
when those events aren't reachable.

## Dependencies

- **Markdig** (BSD-2-Clause) — Markdown parsing. Shipped as compiled DLL targeting .NET Standard 2.0.
- **ColorCode.Core** (MIT) — Syntax highlighting. Shipped as compiled DLL targeting .NET Standard 1.4.
- **Inter** (SIL OFL) — Body font. Shipped as .ttf files.
- **JetBrains Mono** (SIL OFL) — Code font. Shipped as .ttf files.

All dependencies are permissively licensed and can be bundled in the package.

## Click-to-Copy

**Fenced code blocks** have a copy button (`CodeBlockCopyButton`).

**Inline code** is rendered as a click-to-copy chip via `InlineFlowBuilder.EmitChip`. Clicking copies the code content to the clipboard and briefly changes the tooltip to "Copied!". Inline code chips are only used inside `InlineFlowBuilder` flows (paragraphs with links or inline code); plain paragraphs still use a single rich-text Label.

```csharp
// Inline code chip (InlineFlowBuilder.EmitChip)
var chip = new Label(content) { enableRichText = false };
chip.AddToClassList("md-code-inline");
chip.RegisterCallback<ClickEvent>(_ => {
    GUIUtility.systemCopyBuffer = content;
    chip.tooltip = "Copied!";
    chip.schedule.Execute(() => chip.tooltip = "Click to copy").StartingIn(1000);
});
```

## Inspector Integration

```csharp
[CustomEditor(typeof(TextAsset))]
public class MarkdownInspector : Editor {
    private UIMarkdownRenderer _renderer;

    public override VisualElement CreateInspectorGUI() {
        if (!IsMarkdownAsset(out _)) return CreateTextPreview(); // .txt/.json/... fallback

        var root = new VisualElement();
        _renderer = new UIMarkdownRenderer();
        root.Add(_renderer.RootElement);
        ThemeManager.Register(root);

        TryRender(force: true);
        // Poll every 300ms, but only re-read + re-render when the file's
        // write-time/size (then content) actually changed.
        root.schedule.Execute(() => TryRender(force: false)).Every(300);
        return root;
    }
}
```

The standalone `MarkdownViewerWindow` EditorWindow auto-loads `.md` files via
`Selection.selectionChanged`, accepts a dropped `.md` file, and live-refreshes through
a `FileSystemWatcher` (coalesced and change-detected, with the same content guard).

## Security

Link URL policy (`UrlPolicy`, ported from kmd's `sanitize.ts`):

- Block `javascript:`, `vbscript:`, `data:`, `file:`, and unknown custom schemes;
  allow `http`/`https`/`mailto`, in-document fragments, and relative paths.
- When click handling is available (best-effort, see Minimum Unity Version),
  `LinkActivation` routes targets: fragments scroll, external URLs open via
  `Application.OpenURL()` (OS browser, never inside the reader), relative paths
  open/select the target. Blocked schemes are rendered as plain text and never linked.

Image policy (`ImagePolicy`) — images are side-effectful on render, so they get an
equivalent gate:

- `search:` AssetDatabase lookups and local paths under the project / document
  directory load by default.
- Remote `http(s)` images and absolute paths outside the project are **blocked unless**
  the user opts in via **Preferences ▸ Kmd Markdown**. So opening a document never
  performs an unsolicited network request or out-of-project file read.
- Downloaded textures are cached by URI for the session and destroyed on domain reload;
  the loader owns/frees only textures it created (AssetDatabase textures are borrowed).

No `<script>`, no event handlers, no raw HTML rendering (Markdig outputs a structured
AST, not HTML).
