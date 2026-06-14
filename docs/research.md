# kmd-unity Research Notes

Research conducted 2026-06-14. Key findings that informed architecture decisions.

## WebView Embedding: Not Viable

Unity has **no public API** for embedding webviews in editor panels. CEF was removed in Unity 2020.1. Options investigated:

| Approach | Status | Reason |
|---|---|---|
| Unity internal `WebView` (reflection) | ⚠️ Fragile | Undocked-only, breaks on dock, version-dependent |
| gree/unity-webview | ⚠️ Overlay | Renders on top of game view, not in EditorWindow |
| Vuplex 3D WebView ($149) | ⚠️ Texture | Needs custom EditorWindow + input forwarding |
| Tauri window reparenting | ❌ | Different window system, no mechanism |
| WKWebView/WebView2 native plugin | ❌ | No maintained project, would need per-platform code |

**Decision**: Rebuild reader natively in UIToolkit. No WebView dependency.

## MarkdownRenderer (UnityGuillaume) Analysis

[Source](https://github.com/UnityGuillaume/MarkdownRenderer) — the best existing Unity Markdown package.

**What it does well:**
- Clean Markdig → UIToolkit pipeline (extends `RendererBase`)
- Custom CSS class support via generic attributes
- Custom USS per-document via YAML front matter
- Image/video async loading
- Header navigation and scroll-to
- Command system for `[text](cmd:commandName)`

**What it lacks (gaps we fill):**
- No GFM tables
- No syntax highlighting (code blocks are plain monospace Labels)
- No copy button
- No task lists
- No autolinks, emphasis extras, alerts
- No outline sidebar
- No dark/light theme switching
- Only uses `UseGenericAttributes()` + `UseYamlFrontMatter()` in pipeline

**Decision**: Build our own renderers on the same Markdig foundation, not fork MarkdownRenderer. Our pipeline is much richer and most renderers need to be written from scratch anyway.

## Syntax Highlighting

**ColorCode.Core** (MIT, .NET Standard 1.4) is the right library:
- Tokenizes source code into colored spans
- ~20 languages built-in (C#, JS, Python, SQL, XML, etc.)
- Decoupled architecture: write a custom `IFormatter` that outputs `<color=#hex>` UIToolkit rich text instead of HTML
- .NET Standard 1.4 is compatible with Unity

No Unity-specific fork needed — we write a `ColorCodeRichTextFormatter` that produces `<color=#569CD6>keyword</color>` strings for `Label.enableRichText = true`.

Performance: a single `Label` per code block with rich text is far cheaper than per-token Labels. Tested approach from Unity community.

## UIToolkit Rich Text

UIToolkit `Label` supports rich text when `enableRichText = true`. Supported tags relevant to us:

| Tag | Use |
|---|---|
| `<b>` `<i>` `<s>` | Bold, italic, strikethrough |
| `<color=#hex>` | Syntax highlighting, inline styling |
| `<link="url">` | Clickable links (2022.2+) |
| `<size=N>` | Font size (absolute, relative, percentage) |
| `<line-height=N>` | Line height (USS has no property for this) |
| `<margin-left=N>` | List indentation |
| `<br>` | Line breaks |

**Critical limitation**: No `line-height` in USS. Must inject `<line-height=1.62>` into Label text. This affects paragraph and heading renderers.

## kmd Reader Source Reference

Files ported from `/home/nyx/.hermes/projects/kmd/`:

| Source File | Port Purpose |
|---|---|
| `src/reader/Reader.tsx` | Two-phase render pattern (not needed in C#), scroll spy logic, link handling |
| `src/reader/Reader.css` | Authoritative style reference for USS port |
| `src/reader/DocumentShell.tsx` | Outline sidebar + scroll container architecture |
| `src/reader/DocumentShell.css` | Layout styles for sidebar |
| `src/reader/codeBlockEnhancements.ts` | Click-to-copy behavior |
| `src/reader/linkPolicy.ts` | URL classification (fragment/internal/external/blocked) |
| `src/reader/resolveAssets.ts` | Image resolution pattern |
| `src/parser/index.ts` | Pipeline feature parity target |
| `src/parser/sanitize.ts` | Security model for C# port |
| `DESIGN.md` | Design token source of truth |

## Math & Mermaid: v2 Scope

**KaTeX**: No C# equivalent exists. Options for v2:
- Pre-render LaTeX to PNG/SVG at build time
- Embed a native math renderer
- Shell out to a LaTeX CLI tool

**Mermaid**: Pure JavaScript, cannot run in Unity. Options for v2:
- Show placeholder with source text (v1)
- Pre-render diagrams at build time
- Shell out to `mmdc` (Mermaid CLI) at edit time

For v1, both render styled placeholders with the source text visible.

## Alternative Approaches Considered

| Approach | Verdict |
|---|---|
| kmd as localhost server + reflection WebView | Fragile, undocked-only, two-process dependency |
| kmd as localhost server + Vuplex texture | Commercial, needs input forwarding glue |
| kmd as localhost server + system browser | Not in-editor, breaks workflow |
| Fork MarkdownRenderer | Most renderers need rewriting anyway |
| Build from scratch on Markdig | ✅ Chosen — full control, clean architecture |