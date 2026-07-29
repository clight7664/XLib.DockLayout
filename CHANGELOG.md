# Changelog

## 0.1.0 - 2026-07-24

- Created the first controlled XLib.DockLayout implementation baseline.
- Added the RCL, standalone WASM Playground, xUnit/bUnit tests, Playwright tests, XML serialization, local persistence abstraction, CI scripts, SBOM, and delivery documentation.
- Environment validation is recorded separately and must not be inferred from this changelog.

## Unreleased - 2026-07-24

- Replaced the RCL `Microsoft.AspNetCore.App` FrameworkReference with `Microsoft.AspNetCore.Components.Web` to support `browser-wasm`.
- Fixed Razor directive formatting in `TextEditorContent.razor`.
- Renamed command queue lambda parameters to avoid discard-variable conflicts.
- Centered splitter collapse controls with absolute positioning and flex alignment.
- Added the full-screen `/standalone` drag-layout page and `EmptyLayout`.
- Changed project-wide warning escalation to `TreatWarningsAsErrors=false` as requested.
- Added a panel-header region menu for Left Top, Left Bottom, Right Top, Right Bottom, Bottom Left, and Bottom Right moves.
- Added toolbar-entry right-click hiding, single-click expand/collapse, and a hidden-panel restore menu.
- Replaced the editor split `AriaLabel` inline Razor interpolation with a helper-backed component expression.
- Added reusable Playground `TreeView` and sortable `RelationTable` components, plus hierarchical and relational demo data.
- Added bUnit source coverage for tab activation, explicit panel moves, and toolbar right-click hiding.

- Added C# state-backed collapse/expand for EditorArea and top-level layout splitters, including XML persistence of collapse state.
- Added recursive binary split-tree compression after an empty Dynamic EditorView is removed, including nested split regression tests.
- Fixed same-view tab reordering so the moved tab remains active.
- Fixed top-level layout splitter pointer initialization and pointer-capture cleanup in the isolated JS module.
- Fixed panel region-menu interactions being intercepted by native panel drag handling; moving a panel now expands and activates it in the target Region.
- Added fixed-position EditorTab context menus with close variants and right/down split operations.
- Added Explorer file-node double-click to open or activate a de-duplicated EditorTab.
- Added opt-in Dark/Light themes and configurable splitter collapse controls to `DockLayoutOptions`.
- Added a Rider-inspired collapsible menu/breadcrumb header with hidden Tool Window restoration and integrated tool entry points.

## Unreleased - 2026-07-29

- Optimized tab activation so activating the current tab is a no-op and does not advance layout revision or emit duplicate activation events.
- Added roving keyboard navigation and explicit `aria-controls` linkage for editor tabs.
- Hardened Rider-style menu state so only one popup is open, Escape/outside click closes it, and hidden tool windows can be restored.
- Added scoped runtime and global Playground scrollbar styling for Firefox and Chromium/WebKit.
- Replaced letter-based branding with a monochrome docking-layout icon in SVG and 32/64/128/256/512 PNG sizes.
- Added NuGet package icon/readme/license metadata plus local publish scripts and release workflow.
- Reorganized the solution with `src`, `samples`, `tests`, `docs`, `assets`, `scripts`, `eng`, `.github`, and Solution Items folders.
- Added GitHub CI, GitHub Pages, release, Dependabot and issue-template configuration.
- Expanded Chinese/English README, contributor guidance, AI reproduction record, repository/Pages guide, NuGet guide and release checklist.
