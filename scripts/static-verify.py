#!/usr/bin/env python3
"""Performs non-compiler structural verification for the source delivery."""
from __future__ import annotations

import json
import re
import sys
import xml.etree.ElementTree as et
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
results: list[tuple[bool, str, str]] = []


def check(name: str, condition: bool, detail: str = "") -> None:
    results.append((condition, name, detail))


required_files = [
    "XLib.DockLayout.sln",
    "src/XLib.DockLayout/XLib.DockLayout.csproj",
    "samples/DockLayout.Playground/DockLayout.Playground.csproj",
    "tests/XLib.DockLayout.Tests/XLib.DockLayout.Tests.csproj",
    "tests/DockLayout.Playground.E2E/DockLayout.Playground.E2E.csproj",
    "docs/08-requirements-traceability.md",
    "SBOM.spdx.json",
    "DELIVERY_INDEX.md",
]
for relative in required_files:
    check(f"exists:{relative}", (ROOT / relative).is_file())
for relative in ["src", "tests", "docs", "assets", "samples", "scripts", "artifacts"]:
    check(f"directory:{relative}", (ROOT / relative).is_dir())

xml_files = list(ROOT.rglob("*.csproj")) + [
    ROOT / "Directory.Build.props",
    ROOT / "Directory.Build.targets",
    ROOT / "Directory.Packages.props",
    ROOT / "NuGet.Config",
    ROOT / "src/XLib.DockLayout/Serialization/layout-v1.xsd",
]
for path in xml_files:
    try:
        et.parse(path)
        check(f"xml-wellformed:{path.relative_to(ROOT)}", True)
    except Exception as error:  # noqa: BLE001 - verification must report all parser failures.
        check(f"xml-wellformed:{path.relative_to(ROOT)}", False, str(error))

for relative in ["global.json", "SBOM.spdx.json"]:
    try:
        json.loads((ROOT / relative).read_text(encoding="utf-8"))
        check(f"json-valid:{relative}", True)
    except Exception as error:  # noqa: BLE001
        check(f"json-valid:{relative}", False, str(error))

trace = (ROOT / "docs/08-requirements-traceability.md").read_text(encoding="utf-8")
rows = re.findall(r"^\| (REQ-(?:F|NF)-\d{3}) \|", trace, flags=re.MULTILINE)
check("traceability-151-unique", len(rows) == 151 and len(set(rows)) == 151, f"rows={len(rows)}, unique={len(set(rows))}")
check("traceability-functional-143", sum(req.startswith("REQ-F-") for req in rows) == 143)
check("traceability-nonfunctional-8", sum(req.startswith("REQ-NF-") for req in rows) == 8)

rcl_project = (ROOT / "src/XLib.DockLayout/XLib.DockLayout.csproj").read_text(encoding="utf-8")
for value in ["AssemblyName", "RootNamespace", "PackageId"]:
    check(f"rcl-identity:{value}", f"<{value}>XLib.DockLayout</{value}>" in rcl_project)
playground_project = (ROOT / "samples/DockLayout.Playground/DockLayout.Playground.csproj").read_text(encoding="utf-8")
check("playground-project-reference", "../../src/XLib.DockLayout/XLib.DockLayout.csproj" in playground_project)
check("standalone-layout-exists", (ROOT / "samples/DockLayout.Playground/Layout/EmptyLayout.razor").exists())
standalone_page = ROOT / "samples/DockLayout.Playground/Pages/StandaloneDrag.razor"
check("standalone-page-exists", standalone_page.exists())
if standalone_page.exists():
    standalone_text = standalone_page.read_text(encoding="utf-8")
    check("standalone-route", '@page "/standalone"' in standalone_text)
    check("standalone-fullscreen", 'width: 100vw; height: 100vh' in standalone_text)
    check("standalone-multiple-tabs", standalone_text.count("EditorTabDefinition") >= 2 or standalone_text.count("new()") >= 10)
check("target-net10", "<TargetFramework>net10.0</TargetFramework>" in (ROOT / "Directory.Build.props").read_text(encoding="utf-8"))
check("central-package-management", "<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>" in (ROOT / "Directory.Packages.props").read_text(encoding="utf-8"))
rcl_xml = et.fromstring(rcl_project)
rcl_package_references = {
    element.attrib.get("Include", "")
    for element in rcl_xml.findall(".//PackageReference")
}
allowed_rcl_packages = {"Microsoft.AspNetCore.Components.Web"}
check(
    "rcl-runtime-package-boundary",
    rcl_package_references == allowed_rcl_packages,
    ", ".join(sorted(rcl_package_references)),
)

blocked_names = {"bin", "obj", "node_modules", "TestResults", ".vs", ".idea"}
blocked = [str(path.relative_to(ROOT)) for path in ROOT.rglob("*") if path.is_dir() and path.name in blocked_names]
check("no-blocked-delivery-directories", not blocked, ", ".join(blocked))
check("no-node-modules", not any(path.name == "node_modules" for path in ROOT.rglob("*")))
check("no-npm-manifest", not any((ROOT / name).exists() for name in ["package.json", "package-lock.json", "pnpm-lock.yaml", "yarn.lock"]))

implementation_files = list((ROOT / "src").rglob("*.cs")) + list((ROOT / "src").rglob("*.razor")) + list((ROOT / "src").rglob("*.js"))
empty = [str(path.relative_to(ROOT)) for path in implementation_files if not path.read_text(encoding="utf-8").strip()]
check("no-empty-source-files", not empty, ", ".join(empty))
markers = re.compile(r"\b(?:TODO|FIXME|NotImplementedException)\b|其余类似|placeholder implementation", re.IGNORECASE)
marked = [str(path.relative_to(ROOT)) for path in implementation_files if markers.search(path.read_text(encoding="utf-8"))]
check("no-placeholder-markers-in-implementation", not marked, ", ".join(marked))

css_class_pattern = re.compile(r'class\s*=\s*"([^"@]*)"')
css_violations: list[str] = []
for path in (ROOT / "src/XLib.DockLayout/Components").rglob("*.razor"):
    for match in css_class_pattern.finditer(path.read_text(encoding="utf-8")):
        for css_class in match.group(1).split():
            if css_class and not css_class.startswith("xdl-") and not css_class.startswith("@("):
                css_violations.append(f"{path.name}:{css_class}")
check("rcl-css-class-prefix", not css_violations, ", ".join(css_violations))

js = (ROOT / "src/XLib.DockLayout/wwwroot/js/xlib-dock-layout.js").read_text(encoding="utf-8")
for export_name in ["initialize", "beginTabDrag", "beginPanelDrag", "beginSplitterDrag", "measure", "observeResize", "setVisibilityObserver", "destroy"]:
    check(f"js-export:{export_name}", f"export function {export_name}" in js)
check("js-no-window-global-assignment", not re.search(r"\b(?:window|globalThis)\s*\.[A-Za-z_$][\w$]*\s*=", js))
check("js-prefix-internal-identifiers", "xlibDockLayoutInstances" in js and "xlibDockLayoutDirection" in js)
check("editor-tree-model", (ROOT / "src/XLib.DockLayout/Models/EditorTreeModels.cs").is_file() and "EditorSplitInfo" in js.replace("EditorSplitInfo", "") + (ROOT / "src/XLib.DockLayout/Models/EditorTreeModels.cs").read_text(encoding="utf-8"))
check("xml-security-dtd-prohibited", "DtdProcessing.Prohibit" in (ROOT / "src/XLib.DockLayout/Services/DockLayoutSerializer.cs").read_text(encoding="utf-8"))
check("xml-security-resolver-null", "XmlResolver = null" in (ROOT / "src/XLib.DockLayout/Services/DockLayoutSerializer.cs").read_text(encoding="utf-8"))
check("command-queue-present", "DockLayoutCommandQueue" in (ROOT / "src/XLib.DockLayout/DockLayoutController.cs").read_text(encoding="utf-8"))
check("geometry-csharp-authority", "ResizeLayoutSplitterAsync" in (ROOT / "src/XLib.DockLayout/Abstractions/IDockLayoutHandle.cs").read_text(encoding="utf-8"))

editor_node = (ROOT / "src/XLib.DockLayout/Components/EditorNodeView.razor").read_text(encoding="utf-8")
check("editor-split-aria-helper", 'AriaLabel="@GetSplitAriaLabel(split.Id)"' in editor_node)
check("editor-split-id-expression", 'SplitterId="@split.Id"' in editor_node and 'SplitterId="split.Id"' not in editor_node)
check("editor-split-no-inline-aria-interpolation", 'AriaLabel="Resize editor split @split.Id"' not in editor_node)

panel_view = (ROOT / "src/XLib.DockLayout/Components/DockPanelView.razor").read_text(encoding="utf-8")
check("panel-move-menu-six-regions", panel_view.count("DockRegionName.") >= 12 and "xdl-panel-move-menu" in panel_view)
check("panel-move-callback", "EventCallback<PanelDragCommit> OnMove" in panel_view)

toolbar_entry = (ROOT / "src/XLib.DockLayout/Components/DockToolBarEntry.razor").read_text(encoding="utf-8")
check("toolbar-right-click-hide", "@oncontextmenu" in toolbar_entry and "Hide from toolbar" in toolbar_entry)
toolbar = (ROOT / "src/XLib.DockLayout/Components/DockToolBar.razor").read_text(encoding="utf-8")
check("toolbar-hidden-entry-restore", "HiddenPanels" in toolbar and "OnShow" in toolbar)
check("toolbar-visibility-independent-state", "ToolBarVisible" in (ROOT / "src/XLib.DockLayout/Models/DockSnapshots.cs").read_text(encoding="utf-8"))
check("toolbar-visibility-public-api", "SetToolBarEntryVisibilityAsync" in (ROOT / "src/XLib.DockLayout/Abstractions/IDockLayoutHandle.cs").read_text(encoding="utf-8"))
serializer_text = (ROOT / "src/XLib.DockLayout/Services/DockLayoutSerializer.cs").read_text(encoding="utf-8")
check("toolbar-visibility-xml", "toolBarVisible" in serializer_text)

check("playground-treeview-component", (ROOT / "samples/DockLayout.Playground/Components/Data/TreeView.razor").is_file())
check("playground-relation-table-component", (ROOT / "samples/DockLayout.Playground/Components/Data/RelationTable.razor").is_file())
check("playground-relation-panel", "RelationTableContent" in (ROOT / "samples/DockLayout.Playground/Program.cs").read_text(encoding="utf-8"))

# P0-P5 implementation checks added for the full interaction repair batch.
store_text = (ROOT / "src/XLib.DockLayout/State/DockLayoutStore.cs").read_text(encoding="utf-8")
handle_text = (ROOT / "src/XLib.DockLayout/Abstractions/IDockLayoutHandle.cs").read_text(encoding="utf-8")
controller_text = (ROOT / "src/XLib.DockLayout/DockLayoutController.cs").read_text(encoding="utf-8")
splitter_text = (ROOT / "src/XLib.DockLayout/Components/DockSplitter.razor").read_text(encoding="utf-8")
root_component_text = (ROOT / "src/XLib.DockLayout/Components/DockLayout.razor").read_text(encoding="utf-8")
tab_header_text = (ROOT / "src/XLib.DockLayout/Components/EditorTabHeader.razor").read_text(encoding="utf-8")
options_text = (ROOT / "src/XLib.DockLayout/Models/DockDefinitions.cs").read_text(encoding="utf-8")
css_text = (ROOT / "src/XLib.DockLayout/wwwroot/css/xlib-dock-layout.css").read_text(encoding="utf-8")
explorer_text = (ROOT / "samples/DockLayout.Playground/Components/Content/ExplorerContent.razor").read_text(encoding="utf-8")
tree_node_text = (ROOT / "samples/DockLayout.Playground/Components/Data/TreeViewNode.razor").read_text(encoding="utf-8")
standalone_text = standalone_page.read_text(encoding="utf-8") if standalone_page.exists() else ""
rider_header_path = ROOT / "samples/DockLayout.Playground/Components/Workbench/RiderWorkbenchHeader.razor"
rider_header_text = rider_header_path.read_text(encoding="utf-8") if rider_header_path.exists() else ""
controller_tests = (ROOT / "tests/XLib.DockLayout.Tests/DockLayoutControllerTests.cs").read_text(encoding="utf-8")
component_tests = (ROOT / "tests/XLib.DockLayout.Tests/DockLayoutComponentTests.cs").read_text(encoding="utf-8")

check("p0-editor-split-toggle-api", "ToggleEditorSplitCollapseAsync" in handle_text and "ToggleEditorSplitCollapseAsync" in controller_text)
check("p0-layout-split-toggle-api", "ToggleLayoutSplitterCollapseAsync" in handle_text and "ToggleLayoutSplitterCollapseAsync" in controller_text)
check("p0-splitter-button-wired", "OnCollapse.InvokeAsync" in splitter_text and "data-xdl-no-drag" in splitter_text)
check("p0-splitter-button-configurable", "ShowSplitterCollapseButtons" in options_text and "EnableSplitterCollapse" in options_text)
check("p0-splitter-button-centered", all(token in css_text for token in ["top: 50%", "left: 50%", "transform: translate(-50%, -50%)"]))
check("p0-splitter-button-no-extra-frame", "border: 0" in css_text and "box-shadow: none" in css_text)
check("p0-dynamic-view-prune", "PruneEmptyDynamicViewsAndCompressTree" in store_text and "RemoveLeaf" in store_text)
check("p0-recursive-prune-test", "ClosingTabsInNestedDynamicSplitsRecursivelyCompressesTheTree" in controller_tests)
check("p0-same-view-reorder-active", "if (!ReferenceEquals(source, destination))" in store_text and "ReorderingWithinTheSameViewKeepsMovedTabActive" in controller_tests)
check("p0-layout-splitter-js-state", "payload.kind === 'splitter' || payload.kind === 'layoutSplitter'" in js)

check("p1-panel-move-expands", all(token in store_text for token in ["panel.Region = target", "panel.Visible = true", "panel.Collapsed = false", "panel.AutoHide = false", "panel.Active = true"]))
check("p1-panel-menu-no-native-drag", ".xdl-panel-move-menu" in js and "[data-xdl-no-drag]" in js)
check("p1-tab-context-actions", "EditorTabContextAction.CloseOthers" in tab_header_text and "EditorTabContextAction.SplitAndMoveRight" in tab_header_text)
check("p1-tab-context-fixed-popup", 'style="@ContextMenuStyle"' in tab_header_text and "position: fixed" in css_text)
check("p1-tab-context-root-handler", "HandleTabContextActionAsync" in root_component_text)
check("p1-tab-context-component-test", "EditorTabContextMenuRaisesCloseAndSplitActions" in component_tests)

check("p2-tree-double-click", "@ondblclick" in tree_node_text and "OnActivated" in tree_node_text)
check("p2-tree-title-helper", 'title="@NodeTitle"' in tree_node_text and "private string NodeTitle" in tree_node_text)
check("p2-tree-selection-expression", 'SelectedItemId="@SelectedItemId"' in tree_node_text and 'SelectedItemId="@SelectedItemId"' in (ROOT / "samples/DockLayout.Playground/Components/Data/TreeView.razor").read_text(encoding="utf-8"))
check("p2-explorer-open-tab", "OpenTabAsync" in explorer_text and "DedupParameter = item.OpenPath" in explorer_text)
check("p2-explorer-cascaded-handle", "XLibDockLayoutHandle" in explorer_text and "CascadingValue" in root_component_text)

check("p3-theme-options", "EnableThemeSupport" in options_text and "DockThemeMode Theme" in options_text)
check("p3-theme-css", "xdl-theme-dark" in css_text and "xdl-theme-light" in css_text)
check("p3-theme-standalone-config", "EnableThemeSupport = true" in standalone_text and "ChangeThemeAsync" in standalone_text)
check("p3-splitter-visibility-component-test", "SplitterCollapseButtonsCanBeHiddenByOptions" in component_tests)

check("p4-rider-header-exists", rider_header_path.is_file())
check("p4-rider-menu-collapsible", "demo-rider-menu-toggle" in rider_header_text and "_expanded" in rider_header_text)
check("p4-rider-breadcrumb", "demo-rider-breadcrumb" in rider_header_text)
check("p4-hidden-tool-restore", "SetToolBarEntryVisibilityAsync(panelId, true)" in rider_header_text and "ExpandPanelAsync(panelId)" in rider_header_text)
check("p4-tool-entry-points", all(token in rider_header_text for token in ["API Console", "Diagnostics", "Persistence", "Accessibility"]))
check("p4-standalone-header-integration", "RiderWorkbenchHeader" in standalone_text)

check("warnings-not-errors", "<TreatWarningsAsErrors>false</TreatWarningsAsErrors>" in (ROOT / "Directory.Build.props").read_text(encoding="utf-8"))

# Productization and repository delivery checks.
product_files = [
    "README.en.md",
    "README.NuGet.md",
    "AGENTS.md",
    "CONTRIBUTING.md",
    "docs/17-ai-reproduction-guide.md",
    "docs/18-github-repository-and-pages.md",
    "docs/19-nuget-publishing.md",
    "docs/20-conversation-implementation-record.md",
    "docs/21-release-checklist.md",
    ".github/workflows/pages.yml",
    ".github/workflows/release.yml",
    ".github/dependabot.yml",
    "scripts/publish-nuget.ps1",
    "scripts/publish-nuget.sh",
]
for relative in product_files:
    check(f"product-file:{relative}", (ROOT / relative).is_file())

solution_text = (ROOT / "XLib.DockLayout.sln").read_text(encoding="utf-8")
for folder in ["src", "samples", "tests", "docs", "assets", "scripts", "eng", ".github", "Solution Items"]:
    check(f"solution-folder:{folder}", f'= "{folder}", "{folder}"' in solution_text)
solution_item_paths = []
for line in solution_text.splitlines():
    stripped = line.strip()
    if " = " not in stripped:
        continue
    left, right = stripped.split(" = ", 1)
    if left == right and ("\\" in left or "/" in left or "." in Path(left).name):
        solution_item_paths.append(left)
missing_solution_items = [item.replace("\\", "/") for item in solution_item_paths if not (ROOT / item.replace("\\", "/")).is_file()]
check("solution-items-exist", not missing_solution_items, ", ".join(missing_solution_items))

branding_dir = ROOT / "assets/branding"
branding_files = [
    "docklayout-icon.svg",
    "docklayout-icon-transparent.svg",
    "docklayout-icon-inverse.svg",
    "docklayout-icon-32.png",
    "docklayout-icon-64.png",
    "docklayout-icon-128.png",
    "docklayout-icon-256.png",
    "docklayout-icon-512.png",
]
for name in branding_files:
    check(f"branding-file:{name}", (branding_dir / name).is_file())
primary_icon = (branding_dir / "docklayout-icon.svg").read_text(encoding="utf-8")
check("branding-no-letter-mark", ">DL<" not in primary_icon and ">XL<" not in primary_icon and "<text" not in primary_icon)
icon_colours = set(re.findall(r"#[0-9a-fA-F]{6}", primary_icon))
check("branding-monochrome", icon_colours.issubset({"#000000", "#ffffff"}), ", ".join(sorted(icon_colours)))

def png_size(path: Path) -> tuple[int, int] | None:
    data = path.read_bytes()
    if len(data) < 24 or data[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    return int.from_bytes(data[16:20], "big"), int.from_bytes(data[20:24], "big")

for size in [32, 64, 128, 256, 512]:
    actual = png_size(branding_dir / f"docklayout-icon-{size}.png")
    check(f"branding-png-size:{size}", actual == (size, size), str(actual))

check("nuget-package-icon", "<PackageIcon>docklayout-icon.png</PackageIcon>" in rcl_project and "docklayout-icon-128.png" in rcl_project)
check("nuget-package-readme", "<PackageReadmeFile>README.NuGet.md</PackageReadmeFile>" in rcl_project)
check("nuget-repository-url", "https://github.com/clight7664/XLib.DockLayout" in rcl_project)
check("nuget-symbol-package", "<IncludeSymbols>true</IncludeSymbols>" in rcl_project and "<SymbolPackageFormat>snupkg</SymbolPackageFormat>" in rcl_project)

readme_text = (ROOT / "README.md").read_text(encoding="utf-8")
for token in ["快速开始", "NuGet", "GitHub Pages", "DockLayoutOptions", "Explorer", "Splitter"]:
    check(f"readme-section:{token}", token in readme_text)
check("readme-branding-icon", "assets/branding/docklayout-icon-128.png" in readme_text)

check("tab-active-no-op", "string.Equals(located.Value.View.ActiveTabId, tabId, StringComparison.Ordinal)" in store_text)
check("tab-active-no-op-test", "ActivatingTheAlreadyActiveTabDoesNotAdvanceTheRevision" in controller_tests)
check("tab-roving-navigation", all(token in tab_header_text for token in ["ArrowLeft", "ArrowRight", "Home", "End", "aria-controls"]))
check("tab-strip-parameter-bindings", all(token in (ROOT / "src/XLib.DockLayout/Components/EditorTabStrip.razor").read_text(encoding="utf-8") for token in ['Tab="@tab"', 'ContentPanelId="@contentPanelId"', '@key="tab.Id"']))

playground_css = (ROOT / "samples/DockLayout.Playground/wwwroot/css/app.css").read_text(encoding="utf-8")
check("runtime-scrollbar-scoped", ".xdl-layout *::-webkit-scrollbar" in css_text and "scrollbar-width: thin" in css_text)
check("playground-scrollbar-global", "*::-webkit-scrollbar" in playground_css and "--demo-scroll-thumb" in playground_css)
check("menu-single-active-state", "private string? _activeMenu" in rider_header_text and "ToggleMenuAsync" in rider_header_text)
check("menu-outside-dismiss", "demo-rider-menu-dismiss" in rider_header_text and '@onclick="CloseMenus"' in rider_header_text)
check("menu-escape-close", 'args.Key == "Escape"' in rider_header_text)
check("menu-aria-controls", 'aria-controls="@MenuPopupId' in rider_header_text and 'aria-haspopup="menu"' in rider_header_text)

pages_text = (ROOT / ".github/workflows/pages.yml").read_text(encoding="utf-8")
release_text = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")
ci_text = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
check("github-pages-workflow", "actions/deploy-pages@v4" in pages_text and "404.html" in pages_text and ".nojekyll" in pages_text)
check("github-ci-workflow", all(token in ci_text for token in ["dotnet restore", "dotnet format", "dotnet build", "dotnet test", "dotnet publish", "dotnet pack"]))
check("github-release-workflow", "NUGET_API_KEY" in release_text and "dotnet nuget push" in release_text and "--skip-duplicate" in release_text)
check("github-no-credential-in-source", not re.search(r"(?i)(?:nuget|github)[_-]?(?:api[_-]?)?(?:key|token)\s*[:=]\s*['\"][A-Za-z0-9_-]{16,}", "\n".join(path.read_text(encoding="utf-8", errors="ignore") for path in ROOT.rglob("*") if path.is_file() and ".git" not in path.parts)))

old_icon_refs = []
for path in ROOT.rglob("*"):
    if path.is_file() and ".git" not in path.parts and path.suffix.lower() in {".md", ".xml", ".json", ".html", ".razor", ".csproj", ".yml", ".yaml", ".css"}:
        if "xlib-docklayout.svg" in path.read_text(encoding="utf-8", errors="ignore"):
            old_icon_refs.append(str(path.relative_to(ROOT)))
check("no-old-letter-icon-reference", not old_icon_refs, ", ".join(old_icon_refs))

for passed, name, detail in results:
    print(f"{'PASS' if passed else 'FAIL'} | {name} | {detail}")
failures = sum(not passed for passed, _, _ in results)
print(f"\nSUMMARY: PASS={len(results) - failures} FAIL={failures}")
sys.exit(1 if failures else 0)
