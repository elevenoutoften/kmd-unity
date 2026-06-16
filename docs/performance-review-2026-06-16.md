# kmd-unity Performance Review - 2026-06-16

Scope: Flow review lane for `kmd-unity`, with emphasis on memory use, scroll-time cost, CPU/GPU pressure, and keeping fixes small.

## Review Decision

Do not close the review lane as clean yet. The implementation has several good recent improvements, including a shared Markdig pipeline, no-op file refresh guards, image request coalescing, owned texture cleanup on domain reload, and theme hook unregistering. The remaining issues are concentrated in view-tree size, active-heading polling, unbounded image texture retention, and missing performance tests.

## Findings

### P1 - Inline flow multiplies UI Toolkit elements per paragraph

`ParagraphBlockRenderer` sends any paragraph containing inline code, links, or autolinks into `InlineFlowBuilder`. `InlineFlowBuilder` then splits every plain text run into word-sized substrings and creates a new `Label` for each segment. Link text also gets a click callback on each emitted word segment.

This is the most likely source of heavy scroll/layout behavior in real Markdown documents. A paragraph with one link can become dozens of elements instead of one label, and a document with many links or inline code can become thousands of extra `VisualElement`/`Label` instances. That increases memory, layout traversal, style matching, text selection setup, hit testing, and repaint work during scroll.

Evidence:

- `Editor/ObjectRenderers/ParagraphBlockRenderer.cs:14-18`
- `Editor/MarkdownReader/InlineFlowBuilder.cs:42-45`
- `Editor/MarkdownReader/InlineFlowBuilder.cs:181-201`
- `Editor/MarkdownReader/InlineFlowBuilder.cs:222-227`

Recommended small fix:

- Do not emit one label per word. Emit coarse plain-text runs and only create separate elements for actual chips/clickable spans.
- For ordinary links, prefer the rich-text link path unless a chip element is required.
- Add a large-document budget test that fails if a generated document with many links creates unbounded element counts.

### P1 - Outline active-heading tracking polls and toggles all items

`DocumentShell` schedules `UpdateActiveHeading` every 250 ms. Each run reads `worldBound` for every outline entry and calls `EnableInClassList` on every button. On a document with many headings, this keeps doing layout/style-sensitive work even when the active heading has not changed.

This is directly in the scroll path. It is smaller than the inline-flow issue, but it will add visible overhead on large documents and low-end machines.

Evidence:

- `Editor/MarkdownReader/DocumentShell.cs:60-62`
- `Editor/MarkdownReader/DocumentShell.cs:108-121`

Recommended small fix:

- Track the last scroll offset and last active index.
- Only recompute when the scroll value changes, and only toggle the old/new active buttons.
- Keep the current 250 ms throttle if needed, but make idle ticks a no-op.

### P1 - Full render tree plus per-label selection prevents low-resource behavior

`UIMarkdownRenderer.Render` clears and rebuilds the full document tree. After every render, `MakeContentSelectable` queries every label and enables per-label selection state. This is acceptable for small documents, but it does not meet the "super fast and low system requirements" bar for long Markdown files.

The immediate problem is amplified by the inline-flow element explosion above: word labels become selection candidates too. A full virtualization rewrite would be overkill right now, but the current design needs hard budgets and cheap fallbacks.

Evidence:

- `Editor/MarkdownReader/UIMarkdownRenderer.cs:91-115`
- `Editor/MarkdownReader/UIMarkdownRenderer.cs:144-158`

Recommended small fix:

- First reduce element count from inline flow.
- Do not make generated inline word labels selectable one by one.
- Add a perf fixture that records element count and render time for a representative long document.

### P2 - Image cache can retain full-resolution native textures for the session

`ImageLoader` caches downloaded/local textures by URI and owns native textures until eviction, cache clear, or domain reload. Local file changes evict stale entries, which is good, but there is no byte-size, pixel-count, or total-cache budget. A Markdown file with large images can keep large textures in native/GPU memory even if they are displayed at max width.

Evidence:

- `Editor/Utilities/ImageLoader.cs:30-38`
- `Editor/Utilities/ImageLoader.cs:173-186`
- `Editor/Utilities/ImageLoader.cs:219-231`

Recommended small fix:

- Add a conservative max image bytes/pixels guard before caching.
- Add a small LRU or total-owned-texture budget.
- Keep large image loading opt-in through the existing image policy/settings path.

### P2 - Fenced code blocks always add a nested horizontal ScrollView

Every fenced code block creates a container, horizontal `ScrollView`, label, and copy button. Known languages also synchronously create rich-text highlighted output. This is reasonable for a few blocks, but many small code blocks pay nested-scroll overhead even when no long line needs horizontal scrolling.

Evidence:

- `Editor/ObjectRenderers/FencedCodeBlockRenderer.cs:13-25`
- `Editor/ObjectRenderers/FencedCodeBlockRenderer.cs:31-35`

Recommended small fix:

- Only wrap in a horizontal scroller when the code has a long line.
- Cache highlighted rich text by skin, language, and content hash.
- Fall back to plain text above a large-code threshold.

### P2 - Review tests are stale and do not cover performance

The current tests still expect link paragraphs to be a `Label` with class `md-paragraph`, but link paragraphs now go through `InlineFlowBuilder` and become a `VisualElement` with child labels. That means the review-lane unit-test card is not ready as written, and there is no large-document performance guard for the scroll/resource complaint.

Evidence:

- `Tests/Editor/MarkdownRendererTests.cs:114-129`
- `Editor/ObjectRenderers/ParagraphBlockRenderer.cs:14-18`

Recommended small fix:

- Update link tests to match the actual inline-flow tree or move ordinary links back to the rich-text path.
- Add tests for element-count budgets on many links, many headings, many code blocks, and large images.

## Flow Disposition

Request changes on the relevant review cards rather than marking the whole review lane done:

- `flow_000803` UIMarkdownRenderer: full-tree rebuild and per-label selection need a perf budget.
- `flow_000806` ParagraphBlockRenderer: inline-flow path is too element-heavy.
- `flow_000809` LinkInlineRenderer: ordinary links should not force word-per-label rendering.
- `flow_000813` FencedCodeBlockRenderer: avoid nested horizontal scrollers for every small code block.
- `flow_000821` DocumentShell: outline active-heading tracking should be scroll-driven and avoid full toggles.
- `flow_000824` ImageLoader: add native texture size/cache limits.
- `flow_000826` Unit tests: update stale link tests and add perf budget tests.

Cards unrelated to the hot path can stay in review, but the package should not be considered performance-acceptable until the P1 items above are fixed.

## Verification

Local Unity Test Runner was not runnable from this checkout because it is a package-only repo: no `ProjectSettings`, no `Packages/manifest.json`, no `.sln`, and no `.csproj` were present. The review above is based on static source inspection and Flow task acceptance criteria.

---

# Rereview - 2026-06-16

Scope: review-lane cards after the follow-up implementation landed, plus the reported runtime symptom that GPU usage still rises sharply while scrolling aggressively.

## Review Decision

The earlier build-time hot spots are mostly improved:

- Inline flow now emits coarse rich-text runs plus real chip/link elements instead of one label per word.
- Fenced code blocks only allocate a nested horizontal `ScrollView` when a line is long.
- Outline tracking no longer toggles every outline item every poll and backs off when unchanged.
- Large-inline-document tests now exercise the inline-flow path.

The remaining GPU spike is not primarily a Markdown parse problem anymore. It is scroll-frame rendering/compositing cost: the viewer still keeps a full UI Toolkit document tree alive, and every aggressive scroll invalidates the viewport so Unity must clip, hit-test, repaint text/backgrounds/borders, sample images, and update hover state as content moves under the pointer.

## Findings

### P1 - Image pixel budget is still not a resource budget

`ImageLoader` defines `MaxTexturePixels`, but both branches after `DownloadHandlerTexture.GetContent` do the same thing: `AddToCache(cacheKey, texture)` and `Owned.Add(texture)`. That prevents the old native-texture leak, but it means an over-budget image is still cached and drawn at full resolution. With `MaxCacheSize = 64`, a document can retain and scroll over many large native/GPU textures. `max-width: 100%` only changes display size; it does not downsample the texture.

Small fix: when `pixels > MaxTexturePixels`, either reject with an error/placeholder and immediately destroy the created texture, or downsample to a bounded texture before applying/caching it. Also prefer a total pixel/byte budget over a count-only LRU.

Evidence:

- `Editor/Utilities/ImageLoader.cs:24-25`
- `Editor/Utilities/ImageLoader.cs:237-253`
- `Editor/Styles/MarkdownReaderDark.uss:249`

### P1 - The renderer is still full-tree, not scroll-virtualized or raster-cached

`UIMarkdownRenderer` renders into one vertical `ScrollView` with one `ContentElement` holding the entire document tree. A single render avoids repeated parsing, but it does not make scroll free. During scroll, UI Toolkit still has to draw the visible part of that tree every frame. Text, images, code blocks, tables, borders, and rounded/background surfaces are not a free static bitmap.

Small fix: do not jump straight to a renderer rewrite. First add a runtime/profiler acceptance task: measure aggressive scroll in Unity Profiler and decide whether the next step is image downsampling, hover-state removal, or block virtualization. If the requirement is truly "super low GPU while flinging long docs", the architectural answer is viewport/block virtualization or page raster caching.

Evidence:

- `Editor/MarkdownReader/UIMarkdownRenderer.cs:38-45`
- `Editor/MarkdownReader/UIMarkdownRenderer.cs:90-118`

### P2 - Hover styles can cause extra style/repaint churn while scrolling under the mouse

The styles still rely on `:hover` for code copy buttons and table rows. When content scrolls aggressively under a stationary pointer, hover targets change continuously. That can trigger style changes and repaints on top of the normal scroll repaint work.

Small fix: for the low-resource mode, remove row hover highlighting and make copy buttons always visible/focus-visible instead of hover-driven opacity changes.

Evidence:

- `Editor/Styles/MarkdownReaderDark.uss:123-148`
- `Editor/Styles/MarkdownReaderDark.uss:153-155`

## Flow Disposition

- `flow_000860`: request changes. Leak is addressed, but the pixel budget is currently a no-op for memory/GPU requirements.
- `flow_000861`: accept by static review. The inline-flow perf budget now covers link/code paragraphs; Unity Test Runner still was not runnable from this checkout.
- `flow_000863`: accept by static review. The one-shot table layout is a reasonable performance tradeoff and documented.
- `flow_000864`: accept by static review. README and architecture docs now accurately scope inline click-to-copy to paragraph flows.

## Why Render Once Is Not Enough

"Render once" means the parse/build step can be avoided until the document changes. It does not mean the GPU has a free immutable picture of the document. A `ScrollView` changes the visible window every frame. The GPU still has to produce new pixels for the window: text glyphs, backgrounds, borders, images, clips, and hover state. To get near-free scrolling, the renderer must either draw far less per frame (virtualize blocks/pages) or scroll a pre-rasterized texture/cache, which trades away normal UI Toolkit interactivity unless extra hit maps/overlays are added.

---

# Fix Applied - 2026-06-16

Implemented the low-risk fixes from the rereview without changing the renderer architecture:

- Oversized downloaded/local/asset images are now rejected before they are cached or displayed. Downloaded over-budget textures are destroyed immediately, and the rejection is remembered by normalized source key so repeated renders do not re-download, re-decode, or re-upload the same expensive image.
- The image cache now has both entry-count and total-pixel budgets, with LRU eviction destroying owned native textures.
- Error image elements clear any previously assigned texture so reused elements do not keep stale GPU resources alive.
- Hover-driven code copy opacity, table-row hover coloring, and outline hover styling were removed from all three themes to avoid scroll-under-pointer style churn.
- Added test friend-assembly visibility so the existing EditMode tests can compile against internal helper methods without making those helpers public API.

Verification:

- `git diff --check` passes, with only existing CRLF normalization warnings.
- Static style scan confirms no remaining `:hover` selectors in `Editor/Styles`.
- Unity 6000.3.8f1 EditMode tests now compile and run from a temporary host project. The suite still fails before renderer assertions because the standalone package import is missing `System.Runtime.CompilerServices.Unsafe, Version=6.0.0.0`, a Markdig runtime dependency. That packaging dependency is separate from this scroll/resource fix and should be handled as its own package dependency task.
