<p align="center">
  <img src="assets/branding/docklayout-icon-128.png" width="112" height="112" alt="DockLayout layout icon" />
</p>

# XLib.DockLayout

`XLib.DockLayout` is a self-developed IDE-style docking Razor Class Library for Blazor WebAssembly. Its C# state model keeps Dock Regions, DockPanels, EditorViews, EditorTabs, ToolBarEntries and split nodes separate. JavaScript is limited to browser primitives such as Pointer Events, Pointer Capture, DOM measurement, ResizeObserver and animation frames.

## Projects

- `src/XLib.DockLayout` — runtime Razor Class Library.
- `samples/DockLayout.Playground` — standalone Blazor WebAssembly consumer.
- `tests/XLib.DockLayout.Tests` — xUnit and bUnit tests.
- `tests/DockLayout.Playground.E2E` — Microsoft.Playwright browser tests.

## Build

```bash
dotnet restore XLib.DockLayout.sln
dotnet build XLib.DockLayout.sln -c Release --no-restore
dotnet test XLib.DockLayout.sln -c Release --no-build
dotnet run --project samples/DockLayout.Playground/DockLayout.Playground.csproj
```

Open `/standalone` for the full-screen workbench.

## Features

- Six dock regions and cross-region panel movement.
- Toolbar entry hide/restore and panel collapse/auto-hide.
- Tab activation, deduplication, sorting, close variants and directional docking.
- Recursive binary split-tree compression after an empty dynamic view is removed.
- Configurable splitter collapse controls.
- Optional dark/light themes and scoped scrollbars.
- Explorer double-click opening, Rider-style menus and breadcrumbs.
- XML import/export, presets and localStorage persistence.

See [README.md](README.md) for the full Chinese guide and [docs/00-document-index.md](docs/00-document-index.md) for engineering documentation.

## Publishing

- GitHub/Pages: [docs/18-github-repository-and-pages.md](docs/18-github-repository-and-pages.md)
- NuGet: [docs/19-nuget-publishing.md](docs/19-nuget-publishing.md)

The repository contains workflow configuration, but this source delivery does not claim that a remote repository or NuGet package has already been published.
