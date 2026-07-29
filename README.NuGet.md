# XLib.DockLayout

IDE-style DockLayout Razor Class Library for Blazor WebAssembly.

## Features

- Six Dock Regions and movable DockPanels.
- EditorView/EditorTab activation, close, split and directional docking.
- C#-owned binary split tree with automatic empty-view compression.
- Configurable Splitter collapse/expand controls.
- Optional Dark/Light themes.
- XML import/export, presets and localStorage persistence.
- No third-party docking/layout runtime dependency.

## Install

```bash
dotnet add package XLib.DockLayout --version 0.1.0
```

```csharp
using XLib.DockLayout.DependencyInjection;
builder.Services.AddXLibDockLayout();
```

```html
<link rel="stylesheet" href="_content/XLib.DockLayout/css/xlib-dock-layout.css" />
```

Documentation and Playground source are available in the GitHub repository declared by the package metadata.
