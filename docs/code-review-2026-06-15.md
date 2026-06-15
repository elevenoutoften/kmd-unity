# Code Review - 2026-06-15

Repository: `D:\Projects\kmd-unity`  
Reviewed head: `650293f`  
Scope: static review of the Unity editor package code, tests, docs, package metadata, and bundled asset layout.

## Executive Summary

The package is small, cohesive, and generally shaped well for an editor-only Unity Markdown reader. The renderer-per-Markdig-node split is a good level of abstraction for the current size, the editor/runtime boundary is clear, the bundled DLLs are editor-only in their `.meta` files, and the code avoids broad framework churn.

The main issues are not "rewrite the architecture" issues. They are concrete feature, safety, lifecycle, and verification gaps:

- Links are rendered as rich-text `<link>` tags, but there is no click handler, so clickable links, heading fragments, relative links, and footnote jumps appear incomplete.
- Image loading bypasses the URL policy and automatically performs remote HTTP(S) fetches and local file reads when Markdown is rendered.
- Async image requests/textures have no lifecycle management or cache, which can create repeated downloads and native texture churn during live refresh.
- File change refresh paths read files without IO exception handling, which is fragile with common editor save/write races.
- Tests and docs are partly stale versus the current implementation, especially rich-text escaping, inline-code copy, link callbacks, and renderer names.

No broad SOLID refactor is recommended right now. Fix the feature gaps and tighten the edges first.

## Verification Notes

- Inspected source with `rg`, `Get-Content`, and git state.
- Working tree was clean before creating this report.
- Did not run Unity Test Runner: this checkout is a UPM-style package without `ProjectSettings`, `.sln`, or `.csproj` files. Unity editors are installed locally, but there is no host Unity project in the repository to run editor tests directly.

## Findings

### P1 - Link tags are emitted but never handled

Evidence:

- `Editor/ObjectRenderers/LinkInlineRenderer.cs:24-31` emits `<link="...">` rich text for safe URLs.
- `Editor/ObjectRenderers/FootnoteLinkRenderer.cs:23` emits a fragment link for footnotes.
- `README.md:48` claims links are clickable.
- `docs/architecture.md:55`, `docs/architecture.md:88`, and `docs/architecture.md:243` describe click callbacks / `Application.OpenURL()`.
- Search found no `PointerClickLinkTagEvent`, `PointerUpLinkTagEvent`, `Application.OpenURL`, or link callback registration.

Impact:

External links, autolinks, relative links, heading fragments, and footnote links are visually marked but do not have the behavior promised by the package docs. The current tests only assert that a `<link` tag exists, so this gap can pass CI unnoticed.

Recommendation:

Add one centralized link activation path instead of per-renderer click logic. Register link-tag events on generated labels or the document root, classify the target through `UrlPolicy`, then:

- `Fragment`: call `ScrollToHeading(id)`.
- `External`: call `Application.OpenURL(url)`.
- `Relative`: resolve against `BaseDirectory` or `AssetDatabase`, then open/select the target safely.
- `Blocked`: ignore or render as plain text.

Add editor tests around classification-to-action behavior. The handler can be unit-tested if extracted behind a small interface, without overbuilding the renderer.

### P1 - Image loading bypasses URL policy and performs automatic network/file access

Evidence:

- `Editor/ObjectRenderers/LinkInlineRenderer.cs:14-20` sends image URLs straight to `ImageLoader.Load(...)`.
- `Editor/Utilities/ImageLoader.cs:49-82` accepts HTTP(S), otherwise resolves the value as a filesystem path and loads it via `file:///...`.
- `UrlPolicy` is applied to normal links, but not to images.

Impact:

Opening a Markdown file can automatically make remote network requests and read arbitrary local image paths. For trusted local docs this is convenient. For untrusted Markdown, it is a privacy/security risk and a surprising side effect inside the Unity editor.

Recommendation:

Define an explicit image policy. A conservative default would allow project-relative/package-relative paths and `search:` assets, while requiring an opt-in preference for remote HTTP(S) images. Consider rejecting absolute filesystem paths outside the document directory or project unless explicitly enabled. Add size/timeout limits for remote images.

### P2 - Async image requests and downloaded textures have no lifecycle control

Evidence:

- `Editor/Utilities/ImageLoader.cs:82-95` starts a `UnityWebRequestTexture`, applies the texture in the completion callback, and disposes the request.
- `Editor/MarkdownReader/UIMarkdownRenderer.cs:86-90` clears the current tree on every render.
- `Editor/MarkdownReader/MarkdownInspector.cs:87-102` and `Editor/MarkdownReader/MarkdownViewerWindow.cs:159-168` can trigger repeated renders during polling/live refresh.

Impact:

Every render can refetch and recreate textures for the same image. Completion callbacks can still run after the image element has been removed by a newer render. Downloaded textures are native Unity objects, so repeated refreshes of image-heavy documents can cause avoidable memory churn and potential leaks if owned textures are not destroyed when no longer used.

Recommendation:

Keep this simple:

- Cache successful image textures by normalized URI for the editor session.
- Track whether the target `Image` is still attached before applying a result.
- Add a small owned-texture disposal path when clearing/replacing rendered content, or use a renderer-level image cache with explicit cleanup.
- Add request timeout and failure handling.

### P2 - Live file refresh can throw during common save races

Evidence:

- `Editor/MarkdownReader/MarkdownViewerWindow.cs:150-168` correctly moves `FileSystemWatcher` events back to the main thread, but the render call is immediate.
- `Editor/MarkdownReader/MarkdownViewerWindow.cs:35-43` calls `_renderer?.RenderFile(_currentPath)`.
- `Editor/MarkdownReader/UIMarkdownRenderer.cs:174-176` calls `File.ReadAllText(path)` with no IO handling.
- `Editor/MarkdownReader/MarkdownInspector.cs:87` also calls `File.ReadAllText(fullPath)` with no IO handling.

Impact:

Editors commonly save via temporary files, partial writes, file locks, or multiple rapid write events. A read during that window can throw `IOException`, `UnauthorizedAccessException`, or similar exceptions from an editor update/scheduled callback. That makes live refresh feel flaky and can spam the Unity console.

Recommendation:

Catch expected IO exceptions around file reads and either keep the last successful render or show a concise transient error. A short debounce/retry on the viewer path would be enough; no complex file-watcher framework is needed.

### P2 - Tests are stale and do not cover the riskiest behavior

Evidence:

- `Tests/Editor/MarkdownRendererTests.cs:49-55` expects `&lt;` and `&amp;`.
- `Editor/MarkdownReader/UIMarkdownRenderer.cs:273-284` documents a different strategy: only neutralize `<` for UI Toolkit rich text and leave `&` untouched.
- `Tests/Editor/MarkdownRendererTests.cs:119-124` checks that safe links emit `<link`, but not that any click behavior exists.
- There are no visible tests for image URL policy, file-read failures, link actions, async image failure handling, theme registration cleanup, or docs examples.

Impact:

The suite can fail on intentional renderer behavior while missing higher-risk regressions. It gives a false sense of coverage around links because tag emission is not the same as user-visible click behavior.

Recommendation:

Update the escaping test to match the current UI Toolkit strategy, or rename/rewrite the production method if HTML-entity escaping is actually desired. Add focused tests for `UrlPolicy`, link-action routing, image policy, and IO error handling. Keep renderer DOM tests lightweight.

### P3 - Documentation has drifted from implementation

Evidence:

- `README.md:40-41` and `docs/architecture.md:78`, `docs/architecture.md:209-212` claim inline code click-to-copy.
- `Editor/ObjectRenderers/CodeInlineRenderer.cs:21-23` emits rich text inside the paragraph label, with no click target.
- `docs/architecture.md:55`, `docs/architecture.md:88`, and `docs/architecture.md:243` describe clickable link callbacks / `Application.OpenURL()`, but no such implementation exists.
- `docs/architecture.md:82` says task lists are read-only `Toggle` controls, while `Editor/ObjectRenderers/TaskListInlineRenderer.cs` emits rich-text checkbox glyphs.
- `docs/architecture.md:127`, `docs/architecture.md:138`, `docs/architecture.md:144`, and `docs/architecture.md:145` reference stale filenames such as `MarkdownViewer.cs`, `ThematicBreakRenderer.cs`, `FootnoteRenderer.cs`, and `MathRenderer.cs`.
- `README.md:59` still says screenshots are to be added.

Impact:

Docs oversell some v1 capabilities and make maintenance harder because file names and implementation descriptions no longer line up with the tree.

Recommendation:

Either implement the missing behaviors or update the docs to match the current product boundary. The highest-value doc fix is to align the feature matrix with real behavior: links are styled but not activated, inline code is styled but not clickable, task lists are glyph-based, and math is raw/fallback rendering.

### P3 - ThemeManager keeps an editor update hook after all roots unregister

Evidence:

- `Editor/Utilities/ThemeManager.cs:85-100` hooks `EditorApplication.update`.
- `Editor/Utilities/ThemeManager.cs:104-110` removes roots but never unhooks when the set becomes empty.
- `Editor/Utilities/ThemeManager.cs:136-154` keeps polling the editor skin.

Impact:

The per-frame work is tiny, but the static update hook becomes permanent for the editor session once any Markdown UI has been opened. This is low severity, but easy to clean up.

Recommendation:

In `Unregister`, remove dead roots and unhook `EditorApplication.update` when `Roots.Count == 0`. Reset `_hooked` and `_needsApply` at the same time.

### P3 - Syntax-highlight formatting creates avoidable substring allocations

Evidence:

- `Editor/SyntaxHighlighting/ColorCodeRichTextFormatter.cs:172` and `Editor/SyntaxHighlighting/ColorCodeRichTextFormatter.cs:193` call `source.Substring(...)` while walking token scopes.
- `Editor/ObjectRenderers/FencedCodeBlockRenderer.cs:35` creates a new formatter for every highlighted code block.

Impact:

This is not a problem for typical Markdown files, but large code blocks can generate many short-lived strings. The parser and UI tree will dominate in many cases, so this should be treated as a low-risk optimization, not a redesign trigger.

Recommendation:

Replace the substring path with a helper that escapes and appends a span directly. If that becomes awkward because `EscapeRichText` currently takes a string, leave it until profiling shows code-block rendering is a hotspot.

## Architecture / SOLID Notes

- `UIMarkdownRenderer` owning the block stack, text buffer, document, heading registry, and root elements is reasonable at this package size. Splitting every concern now would add ceremony.
- The `MarkdownObjectRenderer` subclasses are small and mostly single-purpose.
- Utility classes (`UrlPolicy`, `ImageLoader`, `ThemeManager`, `LanguageMap`) are a sensible boundary.
- The main SRP pressure is link activation: renderers should not each invent behavior. A shared link action/router would keep the design clean.
- The async image lifecycle is the other boundary worth tightening; it should be owned by a small cache/loader abstraction rather than scattered through renderers.

## Security Notes

- Normal link scheme filtering is a good start.
- Image URLs need equivalent policy treatment because images are side-effectful on render.
- Rendering exceptions currently expose `ex.Message` and `ex.StackTrace` in the document UI (`UIMarkdownRenderer.cs:116-125`). This is acceptable for a local editor tool, but avoid copying stack traces into user-facing screenshots/docs.
- No raw HTML rendering path was found, which keeps the attack surface smaller than an HTML/WebView renderer.

## Test Coverage Notes

Current tests cover basic renderer output classes and a small amount of rich-text output. The missing coverage is concentrated around behavior rather than visual structure:

- Link click handling and fragment navigation.
- Image policy and failed image loads.
- File save race resilience.
- Theme registration/unregistration.
- Documentation examples or showcase smoke rendering.
- Large code block / table rendering smoke tests.

## Documentation Quality Notes

The README is clear and concise, and `docs/architecture.md` explains the technical tradeoffs well. The issue is freshness: several claims now describe an intended implementation rather than the checked-in one. Updating those claims will reduce support/debugging time more than adding new prose.

## Suggested Fix Order

1. Implement central link activation or downgrade docs/tests to say links are styled only.
2. Add an image loading policy and remote image preference.
3. Add lightweight image cache/lifecycle cleanup.
4. Add IO exception handling plus a short file-watch debounce/retry.
5. Bring tests in line with current rich-text escaping and add behavior tests for links/images/IO.
6. Refresh README and architecture docs.
7. Clean up the low-severity ThemeManager hook and substring allocation issues opportunistically.
