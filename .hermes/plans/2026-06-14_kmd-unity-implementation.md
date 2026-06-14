# kmd-unity Implementation Plan

> **For Hermes:** Use subagent-driven-development skill to implement this plan task-by-task.

**Goal:** Port kmd's reader mode to a Unity UIToolkit package that renders `.md` files beautifully in the Unity Editor inspector.

**Architecture:** Markdig parses Markdown → AST → custom `MarkdownObjectRenderer` subclasses map each node to UIToolkit `VisualElement` tree → USS styling (dark/light). ColorCode.Core handles syntax highlighting via `<color=#hex>` rich text tags. No WebView, no external process.

**Tech Stack:** C# / Unity UIToolkit / Markdig (DLL) / ColorCode.Core (DLL) / Inter + JetBrains Mono fonts (SIL OFL)

---

## Phase 0: Scaffolding

Package structure, asmdef, package.json, DLL dependencies, font assets.

### Task 0.1: Create package.json and asmdef

**Files:**
- Create: `package.json`
- Create: `Editor/Kmd.MarkdownReader.Editor.asmdef`

**Step 1: Create package.json**

```json
{
  "name": "com.kmd.markdownreader",
  "displayName": "kmd Markdown Reader",
  "version": "0.1.0",
  "unity": "2022.3",
  "description": "Beautiful Markdown rendering in the Unity Editor inspector.",
  "dependencies": {
    "com.unity.ui": "1.0.0"
  }
}
```

**Step 2: Create Editor asmdef**

```json
{
  "name": "Kmd.MarkdownReader.Editor",
  "rootNamespace": "Kmd.MarkdownReader",
  "references": [],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowAnyPlatform": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

**Step 3: Verify**
- Open Unity package manager, add from disk → `package.json`
- Confirm no compile errors

### Task 0.2: Add Markdig and ColorCode DLLs

**Files:**
- Create: `Plugins/Markdig.dll` (compiled from .NET Standard 2.0)
- Create: `Plugins/ColorCode.Core.dll` (compiled from .NET Standard 1.4)
- Create: `Plugins/Markdig.xml` (intellisense)
- Create: `Plugins/ColorCode.Core.xml` (intellisense)

**Step 1: Compile Markdig**

Download Markdig v0.38+ NuGet package, extract, compile to .NET Standard 2.0 DLL using dotnet CLI:

```bash
dotnet new classlib -n MarkdigBuild -o /tmp/markdig-build --framework netstandard2.0
cd /tmp/markdig-build
dotnet add package Markdig
dotnet build -c Release
cp bin/Release/netstandard2.0/Markdig.dll /path/to/kmd-unity/Plugins/
```

**Step 2: Compile ColorCode.Core**

```bash
dotnet new classlib -n ColorCodeBuild -o /tmp/colorcode-build --framework netstandard1.4
cd /tmp/colorcode-build
dotnet add package ColorCode.Core
dotnet build -c Release
cp bin/Release/netstandard1.4/ColorCode.Core.dll /path/to/kmd-unity/Plugins/
```

**Step 3: Set DLL import settings in Unity**

In Unity Inspector for each DLL:
- Set platform: Any
- Set CPU: Any
- Mark as "Don't process" if Unity complains

**Step 4: Verify**
- Create a test script that `using Markdig;` and `using ColorCode.Core;` — confirm no compile errors

### Task 0.3: Add font assets

**Files:**
- Copy: `Fonts/Inter-Regular.ttf`
- Copy: `Fonts/Inter-SemiBold.ttf`
- Copy: `Fonts/Inter-Bold.ttf`
- Copy: `Fonts/JetBrainsMono-Regular.ttf`
- Copy: `Fonts/JetBrainsMono-Bold.ttf`

**Step 1: Download Inter and JetBrains Mono**
- Inter: https://rsms.me/inter/ (SIL OFL)
- JetBrains Mono: https://www.jetbrains.com/lp/mono/ (SIL OFL)

**Step 2: Copy specific weights**
Only the 5 font files listed above — no variable fonts, no extra weights.

**Step 3: Verify**
- Import into Unity, confirm font assets are created

---

## Phase 1: Core Pipeline

Markdig pipeline setup, main renderer entry point, basic block renderers.

### Task 1.1: Create UIMarkdownRenderer — main entry point

**Files:**
- Create: `Editor/MarkdownReader/UIMarkdownRenderer.cs`

**Implementation:**

```csharp
namespace Kmd.MarkdownReader
{
    public class UIMarkdownRenderer : RendererBase
    {
        public VisualElement RootElement { get; }
        public VisualElement ContentElement { get; }
        private readonly Stack<VisualElement> _blockStack = new();
        
        // Pipeline configuration
        public static MarkdownPipeline CreatePipeline() => new MarkdownPipelineBuilder()
            .UseAutoIdentifiers()
            .UseAutoLinks()
            .UsePipeTables()
            .UseGridTables()
            .UseTaskLists()
            .UseStrikethrough()
            .UseEmphasisExtras()
            .UseFootnotes()
            .UseYamlFrontMatter()
            .UseGenericAttributes()
            .UseAlertBlocks()
            .Build();
        
        // Render entry point
        public VisualElement Render(string markdown) { ... }
        public VisualElement RenderFile(string path) { ... }
    }
}
```

Key behaviors:
- `RootElement` is a `ScrollView` (vertical, elastic)
- `ContentElement` is a `VisualElement` with class `md-body` inside the scroll view
- Block stack pattern from MarkdownRenderer: `StartBlock()` pushes, `FinishBlock()` pops
- Text accumulation: `StartNewText()` creates Label, `WriteText()` appends rich text

**Verify:** Create a test window that calls `renderer.Render("# Hello\n\nWorld")` and shows a heading + paragraph.

### Task 1.2: Create MarkdownInspector — CustomEditor for .md

**Files:**
- Create: `Editor/MarkdownReader/MarkdownInspector.cs`

```csharp
[CustomEditor(typeof(TextAsset))]
public class MarkdownInspector : Editor
{
    private UIMarkdownRenderer _renderer;
    private string _currentPath;

    void OnEnable() { ... }
    public override VisualElement CreateInspectorGUI() { ... }
}
```

Auto-detects `.md` files, loads content, renders. Falls back to default inspector for non-markdown TextAssets.

**Verify:** Click a `.md` file in Unity Project window → inspector shows rendered content.

### Task 1.3: HeadingBlockRenderer

**Files:**
- Create: `Editor/ObjectRenderers/HeadingBlockRenderer.cs`

Renders h1–h6 as Labels with classes `md-h1` through `md-h6`. Registers heading IDs for scroll-to navigation.

### Task 1.4: ParagraphBlockRenderer

**Files:**
- Create: `Editor/ObjectRenderers/ParagraphBlockRenderer.cs`

Renders paragraphs as Labels with class `md-paragraph`. Handles inline content (bold, italic, links, code) via rich text accumulation.

### Task 1.5: EmphasisInlineRenderer

**Files:**
- Create: `Editor/ObjectRenderers/EmphasisInlineRenderer.cs`

Maps `DelimiterCount` to `<b>` (2+), `<i>` (1, not bold), `<s>` (strikethrough from EmphasisExtras).

### Task 1.6: LiteralInlineRenderer

**Files:**
- Create: `Editor/ObjectRenderers/LiteralInlineRenderer.cs`

Appends escaped text content via `WriteText()`.

### Task 1.7: LineBreakInlineRenderer

**Files:**
- Create: `Editor/ObjectRenderers/LineBreakInlineRenderer.cs`

Soft break → space, hard break → `<br>`.

### Task 1.8: CodeInlineRenderer

**Files:**
- Create: `Editor/ObjectRenderers/CodeInlineRenderer.cs`

Inline code as `<mspace>` or styled span with `md-code-inline` class. Click-to-copy via `ClickEvent`.

### Task 1.9: LinkInlineRenderer

**Files:**
- Create: `Editor/ObjectRenderers/LinkInlineRenderer.cs`

Two modes:
- **Image**: Load via `UnityWebRequestTexture`, display as `Image` element
- **Link**: `<link="url">` rich text tag with `PointerClickLinkTagEvent` callback (Unity 2022.2+)

URL policy: fragment scroll, `file://` internal, `https://` external (open in OS browser), block unsafe schemes.

### Task 1.10: ListBlockRenderer

**Files:**
- Create: `Editor/ObjectRenderers/ListBlockRenderer.cs`

Ordered and unordered lists. Nested lists via `<margin-left>` rich text. Task list checkboxes as read-only `Toggle` elements.

### Task 1.11: ThematicBreakBlockRenderer

**Files:**
- Create: `Editor/ObjectRenderers/ThematicBreakBlockRenderer.cs`

`VisualElement` with class `md-hr`, styled as a 1px border.

### Task 1.12: QuoteBlockRenderer

**Files:**
- Create: `Editor/ObjectRenderers/QuoteBlockRenderer.cs`

`VisualElement` with class `md-blockquote`, left border accent via USS.

---

## Phase 2: Code Blocks & Syntax Highlighting

### Task 2.1: ColorCodeRichTextFormatter

**Files:**
- Create: `Editor/SyntaxHighlighting/ColorCodeRichTextFormatter.cs`

Custom `IFormatter` (or direct token walker) that takes ColorCode output and produces `<color=#hex>token</color>` UIToolkit rich text strings.

Two theme palettes: light and dark, selected based on `EditorGUIUtility.isProSkin`.

### Task 2.2: LanguageMap

**Files:**
- Create: `Editor/SyntaxHighlighting/LanguageMap.cs`

Maps Markdown fenced code block language identifiers (e.g., `"csharp"`, `"js"`, `"python"`) to ColorCode `ILanguage` instances.

### Task 2.3: FencedCodeBlockRenderer

**Files:**
- Create: `Editor/ObjectRenderers/FencedCodeBlockRenderer.cs`

Single `Label` with `enableRichText = true`, `white-space: pre`, monospace font class. Language tag extracted from fence info string → ColorCode → `ColorCodeRichTextFormatter` → rich text Label content.

### Task 2.4: CodeBlockRenderer (indented)

**Files:**
- Create: `Editor/ObjectRenderers/CodeBlockRenderer.cs`

Plain monospace Label, no syntax highlighting. Class `md-code`.

### Task 2.5: Copy button on code blocks

**Files:**
- Create: `Editor/Utilities/CodeBlockCopyButton.cs`

Absolute-positioned `Button` in top-right corner of code block container. On click: `GUIUtility.systemCopyBuffer = codeText`, briefly show "✓ Copied" feedback.

### Task 2.6: Inline code click-to-copy

**Files:**
- Modify: `Editor/ObjectRenderers/CodeInlineRenderer.cs`

Register `ClickEvent` on inline code Labels. On click: copy text to `GUIUtility.systemCopyBuffer`.

---

## Phase 3: Tables & Advanced Blocks

### Task 3.1: TableBlockRenderer

**Files:**
- Create: `Editor/ObjectRenderers/TableBlockRenderer.cs`

GFM pipe tables as `VisualElement` grid:
- `VisualElement("md-table-wrapper")` containing `ScrollView` (horizontal)
- Header row: `VisualElement("md-table-header")` with `Label("md-th")` cells
- Body rows: `VisualElement("md-table-row")` with `Label("md-td")` cells
- Column widths proportional to content

### Task 3.2: AlertBlockRenderer

**Files:**
- Create: `Editor/ObjectRenderers/AlertBlockRenderer.cs`

GitHub-style alerts (`>[!NOTE]`, `>[!TIP]`, etc.) as styled `VisualElement`:
- Left border color per type (blue/green/amber/red)
- Title label with alert type name
- Body content rendered recursively
- Classes: `md-alert`, `md-alert-note`, `md-alert-tip`, etc.

### Task 3.3: FootnoteRenderer

**Files:**
- Create: `Editor/ObjectRenderers/FootnoteGroupRenderer.cs`
- Create: `Editor/ObjectRenderers/FootnoteLinkRenderer.cs`

Superscript link in body text, footnote section at document bottom. Uses `<sup>` or `<size=80%>` rich text for superscript.

---

## Phase 4: Styling & Theme

### Task 4.1: Dark theme USS

**Files:**
- Create: `Editor/Styles/MarkdownReaderDark.uss`

Port all CSS custom properties from `Reader.css` to USS, dark theme variant. Key classes:
- `.md-body` — container, max-width 800px, Inter font
- `.md-h1` through `.md-h6` — heading typography scale
- `.md-paragraph` — body text, line-height via `<line-height>` injection
- `.md-codeblock`, `.md-code` — code blocks, JetBrains Mono
- `.md-blockquote` — left border accent
- `.md-table-*` — table cells, headers, borders
- `.md-alert-*` — alert callout colors
- `.md-hr` — thematic break

Design tokens (from `DESIGN.md`):
- Dark surface: `#0D1117`, text: `#E1E4E8`, secondary: `#8B949E`
- Link: `#58A6FF`, border: `#30363D`

### Task 4.2: Light theme USS

**Files:**
- Create: `Editor/Styles/MarkdownReaderLight.uss`

Same structure, light palette:
- Surface: `#FFFFFF`, text: `#1F2328`, secondary: `#656D76`
- Link: `#0969DA`, border: `#D8DEE6`

### Task 4.3: ThemeManager — auto-detect editor skin

**Files:**
- Create: `Editor/Utilities/ThemeManager.cs`

```csharp
public static class ThemeManager
{
    public static bool IsDarkTheme => EditorGUIUtility.isProSkin;
    public static string GetThemeUssPath() => IsDarkTheme 
        ? "MarkdownReaderDark" 
        : "MarkdownReaderLight";
    public static void ApplyTheme(VisualElement root) { ... }
}
```

Loads the appropriate USS via `EditorGUIUtility.Load()` and applies to root element.

---

## Phase 5: Outline & Navigation

### Task 5.1: Outline extraction

**Files:**
- Create: `Editor/MarkdownReader/OutlineExtractor.cs`

Walks Markdig AST, extracts heading text + level + slug into `List<OutlineEntry>`. Called before rendering.

### Task 5.2: DocumentShell — outline sidebar + scroll container

**Files:**
- Create: `Editor/MarkdownReader/DocumentShell.cs`

Two-pane layout:
- Left: `VisualElement("md-outline-sidebar")` with outline entries as buttons
- Right: `ScrollView` containing the rendered content
- Toggle button to show/hide outline
- On narrow screens (<768px width): outline overlays as fixed panel

### Task 5.3: Scroll spy — active heading tracking

**Files:**
- Modify: `Editor/MarkdownReader/DocumentShell.cs`

Register scroll callback on `ScrollView`. Compare `scrollOffset` with registered heading positions to determine active heading. Highlight active heading in outline.

### Task 5.4: Scroll-to-fragment on link click

**Files:**
- Create: `Editor/Utilities/AnchorNavigation.cs`

When a fragment link (`#heading-slug`) is clicked, find the registered heading `Label` and call `ScrollView.ScrollTo()`.

---

## Phase 6: Security & Polish

### Task 6.1: URL policy — sanitize links

**Files:**
- Create: `Editor/Utilities/UrlPolicy.cs`

Port from kmd's `sanitize.ts`:
- Block: `javascript:`, `vbscript:`, `data:` (non-image), `file:` (absolute), unknown schemes
- Allow: `http:`, `https:`, `mailto:`, relative paths
- External links → `Application.OpenURL()`
- Fragment links → scroll-to
- Relative `.md` links → open in viewer (future feature)

### Task 6.2: Image loading

**Files:**
- Create: `Editor/Utilities/ImageLoader.cs`

Async image loading via `UnityWebRequestTexture`. Resolve relative paths against the markdown file's directory. Handle `search:` prefix for AssetDatabase lookups.

### Task 6.3: Error and empty states

**Files:**
- Modify: `Editor/MarkdownReader/UIMarkdownRenderer.cs`

- Parse error → styled error message with stack trace
- Empty file → "This file is empty." message
- File not found → error state

### Task 6.4: README.md preview

**Files:**
- Create: `Editor/MarkdownReader/MarkdownViewer.cs`

`EditorWindow` subclass for standalone `.md` viewing. Dockable, auto-loads selected `.md` file via `Selection.selectionChanged`.

---

## Phase 7: Testing & Docs

### Task 7.1: Unit tests — renderer output

**Files:**
- Create: `Tests/Editor/MarkdownRendererTests.cs`

Test each renderer with known Markdown inputs:
- Headings h1–h6
- Paragraphs with inline formatting
- Code blocks with syntax highlighting
- Tables
- Task lists
- Blockquotes
- Alerts
- Footnotes

### Task 7.2: Update README.md

**Files:**
- Modify: `README.md`

Add installation instructions (UPM git URL), usage examples, screenshot, feature list.

### Task 7.3: Update CHANGELOG.md

**Files:**
- Modify: `CHANGELOG.md`

Document v0.1.0 features.

---

## Open Questions

1. **Math (KaTeX)**: v1 shows styled placeholder with source text. v2 could pre-render LaTeX to PNG/SVG or embed a math font. Decide before Phase 3.
2. **Mermaid**: v1 shows placeholder. v2 could shell out to `mmdc` CLI at edit time. Decide before Phase 3.
3. **Line numbers in code blocks**: UIToolkit Labels don't natively support line numbers. Options: (a) prepend line numbers to text, (b) two-column layout (gutter + code). Decide in Phase 2.
4. **Custom USS per document**: Like MarkdownRenderer's YAML front matter USS override. Decide after Phase 4.
5. **Responsive outline**: Mobile-like outline overlay for narrow Inspector widths. Decide in Phase 5.

## Dependency Map

```
Phase 0 (scaffolding) → all phases depend on this
Phase 1 (core pipeline) → depends on Phase 0
Phase 2 (code blocks) → depends on Phase 1 (FencedCodeBlockRenderer needs pipeline)
Phase 3 (tables & alerts) → depends on Phase 1
Phase 4 (styling) → depends on Phase 1 (renders need USS classes)
Phase 5 (outline) → depends on Phase 1 (needs heading registration)
Phase 6 (security & polish) → depends on Phases 1–4
Phase 7 (testing & docs) → depends on Phases 1–6
```

Phases 2, 3, 4 can run in parallel after Phase 1.
Phases 5 and 6 can partially overlap.
Phase 7 is final.