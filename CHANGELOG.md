# Changelog

All notable changes to kmd-unity will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.1.0]

### Added
- Markdown rendering in the Editor Inspector for `.md` TextAssets, and a dockable
  **Markdown Viewer** window (selection-following, drag-drop, live refresh).
- Block renderers: headings, paragraphs, ordered/unordered/task lists,
  blockquotes, GitHub alerts, GFM tables, fenced/indented code blocks,
  footnotes, horizontal rules.
- Inline renderers: bold/italic/strikethrough, inline code (click-to-copy),
  links (sanitized URL policy) and autolinks, images (async loading), line breaks.
- Syntax highlighting for fenced code via a ColorCode → UIToolkit rich-text
  formatter, plus a hover copy button.
- Dark and light USS themes auto-selected from the Editor skin (ThemeManager).
- Outline sidebar with scroll-position tracking and scroll-to-heading / anchor
  navigation.
- Packaging: bundled Markdig and ColorCode.Core DLLs, Inter and JetBrains Mono
  fonts, committed `.meta` files, and third-party license notices.
- Architecture and research documents.

[0.1.0]: https://github.com/elevenoutoften/kmd-unity/releases/tag/v0.1.0