<p align="center">
  <img src="assets/branding/docklayout-icon-128.png" width="112" height="112" alt="DockLayout layout icon" />
</p>

<h1 align="center">XLib.DockLayout</h1>

<p align="center">
  面向 Blazor WebAssembly 的 IDE 风格 DockLayout Razor Class Library。
</p>

<p align="center">
  <a href="README.en.md">English</a> ·
  <a href="docs/00-document-index.md">文档索引</a> ·
  <a href="docs/18-github-repository-and-pages.md">GitHub Pages</a> ·
  <a href="docs/19-nuget-publishing.md">NuGet 发布</a>
</p>

## 项目定位

`XLib.DockLayout` 提供可组合的 Dock Region、DockPanel、EditorView、EditorTab、ToolBarEntry、Splitter 和布局持久化能力。核心布局状态、业务规则和 XML 兼容逻辑均由 C# 实现；JavaScript 仅处理 Pointer Events、DOM 测量、ResizeObserver、Pointer Capture 和动画帧等浏览器底层能力。

运行时不依赖第三方 Docking、Layout、DragDrop 或完整 UI Framework。

## 主要能力

- 六个固定 Dock Region：Left Top、Left Bottom、Right Top、Right Bottom、Bottom Left、Bottom Right。
- Panel 激活、折叠、Auto-hide、跨 Region 移动，以及 LT/RT ToolBarEntry 显示与恢复。
- EditorTab 打开、关闭、固定、去重、排序、跨 EditorView 拖拽和右键菜单。
- Center Docking、Directional Docking、水平/垂直 EditorView 分屏。
- 关闭 Dynamic EditorView 最后一个 Tab 后递归压缩二叉 Split Tree，不保留空白分割区域。
- Splitter 拖拽、键盘调整、折叠/展开及按钮显隐配置。
- Explorer 双击文件节点打开或激活去重 EditorTab。
- Dark/Light 主题、全局/组件级滚动条样式和 Rider 风格菜单/面包区。
- Content Registry、编程式 API、事件、XML 导入导出、Preset 与 localStorage 持久化。
- xUnit、bUnit、Microsoft.Playwright 测试工程和 GitHub Actions 工作流。

## 环境要求

- .NET SDK `10.0.302` 或兼容的 .NET 10 Patch SDK。
- 支持 WebAssembly 的现代浏览器。
- 执行 E2E 测试时需要 PowerShell 或 Bash，以及 Playwright Chromium。

版本由 `global.json` 和 `Directory.Packages.props` 固定。

## 解决方案结构

```text
XLib.DockLayout/
├─ src/
│  └─ XLib.DockLayout/                 # Razor Class Library，承载全部核心实现
├─ samples/
│  └─ DockLayout.Playground/           # standalone Blazor WebAssembly 演示
├─ tests/
│  ├─ XLib.DockLayout.Tests/           # xUnit + bUnit
│  └─ DockLayout.Playground.E2E/       # Microsoft.Playwright
├─ docs/                               # 需求、架构、设计、发布与 AI 复刻文档
├─ assets/branding/                    # 黑白 DockLayout 图标资产
├─ scripts/                            # 静态验证、Playwright 和发布脚本
├─ eng/                                # 构建/打包入口
├─ artifacts/                          # 本地构建产物，不提交仓库
└─ .github/workflows/                  # CI、Pages 和 NuGet Release
```

Solution 使用 `src`、`samples`、`tests`、`docs`、`assets`、`scripts`、`eng` 和 `.github` Solution Folder，避免项目平铺。

## 快速开始

### 1. 还原、构建和测试

```bash
dotnet --info
dotnet restore XLib.DockLayout.sln
dotnet build XLib.DockLayout.sln -c Release --no-restore
dotnet test XLib.DockLayout.sln -c Release --no-build
```

### 2. 启动 Playground

```bash
dotnet run \
  --project samples/DockLayout.Playground/DockLayout.Playground.csproj \
  -c Release
```

浏览器入口：

- `/`：完整 Playground。
- `/standalone`：仅 DockLayout 工作区、Rider 风格菜单和状态栏。

### 3. 发布静态站点

```bash
dotnet publish \
  samples/DockLayout.Playground/DockLayout.Playground.csproj \
  -c Release \
  -o artifacts/playground
```

## 在 Blazor 项目中引用

### ProjectReference

```xml
<ItemGroup>
  <ProjectReference Include="../XLib.DockLayout/XLib.DockLayout.csproj" />
</ItemGroup>
```

### NuGet

```bash
dotnet add package XLib.DockLayout --version 0.1.0
```

`Program.cs`：

```csharp
using XLib.DockLayout.DependencyInjection;

builder.Services.AddXLibDockLayout();
```

`wwwroot/index.html`：

```html
<link rel="stylesheet"
      href="_content/XLib.DockLayout/css/xlib-dock-layout.css" />
```

## 最小组件示例

```razor
@using XLib.DockLayout.Components
@using XLib.DockLayout.Models

<DockLayout Definition="@Definition"
            Options="@Options"
            Style="width:100%;height:100%;" />

@code {
    private DockLayoutOptions Options { get; } = new()
    {
        EnableThemeSupport = true,
        Theme = DockThemeMode.Dark,
        EnableSplitterCollapse = true,
        ShowSplitterCollapseButtons = true,
        TabOverflowMode = TabOverflowMode.Scroll
    };

    private DockLayoutDefinition Definition { get; } = new()
    {
        Panels =
        [
            new DockPanelDefinition
            {
                Id = "explorer",
                Title = "Explorer",
                Icon = "E",
                Region = DockRegionName.LeftDockUpper,
                ContentId = "explorer"
            }
        ],
        EditorViews =
        [
            new EditorViewDefinition
            {
                Id = "view-main",
                ActiveTabId = "welcome",
                Tabs =
                [
                    new EditorTabDefinition
                    {
                        Id = "welcome",
                        Title = "Welcome",
                        ContentId = "welcome"
                    },
                    new EditorTabDefinition
                    {
                        Id = "readme",
                        Title = "README.md",
                        ContentId = "text-editor",
                        DedupParameter = "/README.md"
                    }
                ]
            }
        ]
    };
}
```

Content 组件必须在 `IDockContentRegistry` 中注册；Playground 的 `Program.cs` 提供完整示例。

## Tab 激活规则

- 单击 Tab 根节点激活，Pin/Close 按钮停止事件冒泡。
- 每个 EditorView 仅有一个 `ActiveTabId`。
- 重复激活当前 Tab 不重复触发 `OnTabActivated`。
- `ArrowLeft`、`ArrowRight`、`Home`、`End` 支持 roving-tabindex 导航。
- `Ctrl+Tab` 在最近操作的 EditorView 中切换；`Ctrl+W` 关闭该 View 的活动 Tab。
- 打开已存在的 Dedup Tab 时激活原 Tab，不创建重复实例。

## 菜单状态规则

- Rider 菜单区由单一 `_activeMenu` 状态控制，同一时刻最多打开一个菜单。
- 再次单击当前按钮、单击遮罩、按 `Esc` 或选择菜单项都会关闭菜单。
- `View → Tool Windows` 每次打开时刷新 C# Snapshot，可恢复被右键隐藏的 ToolBarEntry。
- 菜单仅属于 Playground 集成层，不进入 RCL 核心状态模型。

## 主题与滚动条

显式主题是可选能力：

```csharp
new DockLayoutOptions
{
    EnableThemeSupport = true,
    Theme = DockThemeMode.Light
};
```

RCL 滚动条样式仅作用于 `.xdl-layout` 内部；宿主可覆盖：

```css
.my-layout {
  --xdl-scroll-track: #f4f4f4;
  --xdl-scroll-thumb: #777;
  --xdl-scroll-thumb-hover: #222;
}
```

## NuGet 打包

```bash
dotnet pack \
  src/XLib.DockLayout/XLib.DockLayout.csproj \
  -c Release \
  -o artifacts/packages
```

NuGet 使用 `assets/branding/docklayout-icon-128.png` 作为 `PackageIcon`。完整发布流程见 [docs/19-nuget-publishing.md](docs/19-nuget-publishing.md)。

## GitHub 与 GitHub Pages

仓库初始化、推荐设置、Secrets 和 Pages 配置见 [docs/18-github-repository-and-pages.md](docs/18-github-repository-and-pages.md)。仓库已包含：

- `.github/workflows/ci.yml`
- `.github/workflows/pages.yml`
- `.github/workflows/release.yml`

这些文件是配置资产；本交付不声称已经在用户 GitHub 账号中创建仓库或完成推送。

## 验证

```bash
python3 scripts/static-verify.py
node --check src/XLib.DockLayout/wwwroot/js/xlib-dock-layout.js
dotnet format XLib.DockLayout.sln --verify-no-changes
dotnet build XLib.DockLayout.sln -c Release
dotnet test XLib.DockLayout.sln -c Release --no-build
```

当前交付中的静态验证记录位于 `docs/15-verification-record.md`。只有在对应环境实际执行成功后，才能将动态验证标记为 Passed。

## 工程约束

- Region、DockPanel、EditorView、EditorTab、ToolBarEntry 必须保持独立对象语义。
- RCL 中全部 CSS class 使用 `xdl-` 前缀。
- JS 模块内部标识使用 `xlibDockLayout` 前缀，不向 `window` 注入全局状态。
- 公共 API 必须包含 XML Documentation。
- 禁止把核心状态和布局规则迁移到 Playground 或 JavaScript。
- 禁止提交 `bin`、`obj`、`node_modules`、`TestResults`、Playwright 浏览器缓存。

完整约束见 [AGENTS.md](AGENTS.md) 与 [docs/17-ai-reproduction-guide.md](docs/17-ai-reproduction-guide.md)。

## 许可证

当前 `LICENSE.txt` 明确保留全部权利，尚未选择开源许可证。公开发布或允许第三方再分发前，项目所有者必须正式确认许可证文本。
