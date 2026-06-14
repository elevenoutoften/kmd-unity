# Kawaii MD reader in Unity

Beautiful Markdown rendering in the Unity Editor.

A Unity package that renders `.md` files with kmd-quality layout, typography,
syntax highlighting, and features like click-to-copy — right inside the Unity
Inspector. Native [UIToolkit](https://docs.unity3d.com/Manual/UIElements.html) +
[Markdig](https://github.com/xoofx/markdig) port of the kmd reader mode.

Part of the [kmd](https://github.com/elevenoutoften/kmd) project.

## Requirements

- Unity **2022.3 LTS** or newer (verified on Unity 6).
- Editor-only — the package renders Markdown in the Editor; it does not ship
  runtime code.

## Installation

Unity Package Manager → **Add package from git URL…**:

```
https://github.com/elevenoutoften/kmd-unity.git
```

Pin a specific commit/tag by appending a ref, e.g. `…/kmd-unity.git#v0.1.0`.

## Usage

- **Inspector** — select any `.md` `TextAsset` in the Project window; it renders
  as styled Markdown. Non-`.md` text assets keep their plain-text preview.
- **Viewer window** — **Window → Kmd → Markdown Viewer** opens a dockable
  viewer with an outline sidebar. It follows the current selection, accepts a
  dropped `.md` file, and live-refreshes when the file changes on disk.
- The theme (dark/light) follows the Editor skin automatically.

## Features

- Headings (h1–h6) with the kmd typographic scale
- Paragraphs, **bold**, *italic*, ~~strikethrough~~, `inline code`
  (click to copy)
- Fenced code blocks with syntax highlighting (via ColorCode) and a copy button;
  indented code blocks
- Ordered, unordered, and task lists (read-only checkboxes), with nesting
- Blockquotes and GitHub-style alerts (NOTE / TIP / IMPORTANT / WARNING / CAUTION)
- GFM tables (horizontally scrollable)
- Footnotes with scroll-to references
- Links (sanitized URL policy, clickable) and autolinks
- Images (async load; relative paths, `search:` AssetDatabase lookup, http/https)
- Horizontal rules, empty/error states
- Dark and light themes, auto-detected from the Editor skin
- Outline sidebar with scroll position tracking (viewer window)

Supported Markdig extensions: AutoIdentifiers, AutoLinks, PipeTables, GridTables,
TaskLists, EmphasisExtras, Footnotes, YAML front matter, GenericAttributes,
Alerts, and Math (the Math extension is enabled in the pipeline; a dedicated math
renderer is planned).

> Screenshots: to be added.

## Dependencies

Bundled under `Plugins/` and `Fonts/` (see `Plugins/THIRD-PARTY-NOTICES.md`):

| Component | Version | License |
| --- | --- | --- |
| [Markdig](https://github.com/xoofx/markdig) | 1.3.0 | BSD-2-Clause |
| [ColorCode.Core](https://github.com/CommunityToolkit/ColorCode-Universal) | 2.0.15 | MIT |
| [Inter](https://github.com/rsms/inter) | — | SIL OFL 1.1 |
| [JetBrains Mono](https://github.com/JetBrains/JetBrainsMono) | — | SIL OFL 1.1 |

## License

MIT — see [LICENSE.md](LICENSE.md).
