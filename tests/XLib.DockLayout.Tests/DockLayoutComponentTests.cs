using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using XLib.DockLayout.Abstractions;
using XLib.DockLayout.Components;
using XLib.DockLayout.DependencyInjection;
using XLib.DockLayout.Models;
using DockLayoutComponent = XLib.DockLayout.Components.DockLayout;

namespace XLib.DockLayout.Tests;

public sealed class DockLayoutComponentTests : BunitContext
{
    public DockLayoutComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddXLibDockLayout();
        Services.GetRequiredService<IDockContentRegistry>().Register<TestContent>("test");
    }

    [Fact]
    public void RendersSixRegionsToolbarsTabsAndAria()
    {
        var definition = new DockLayoutDefinition
        {
            Panels = Enum.GetValues<DockRegionName>().Select((region, index) => new DockPanelDefinition { Id = $"panel-{index}", Region = region, ContentId = "test", Title = region.ToString() }).ToArray(),
            EditorViews = new[] { new EditorViewDefinition { Id = "view-main", Tabs = new[] { new EditorTabDefinition { Id = "tab-main", ContentId = "test", Title = "Main" } } } }
        };
        var cut = Render<DockLayoutComponent>(parameters => parameters.Add(component => component.Definition, definition));
        Assert.Equal(6, cut.FindAll("[data-xdl-region]").Count);
        Assert.Equal(2, cut.FindAll("[role=toolbar]").Count);
        Assert.Single(cut.FindAll("[role=tablist]"));
        Assert.Single(cut.FindAll("[role=tab]"));
        Assert.NotEmpty(cut.FindAll("[role=separator]"));
    }

    [Fact]
    public void UnregisteredContentIsolatedToContentArea()
    {
        var definition = new DockLayoutDefinition { EditorViews = new[] { new EditorViewDefinition { Id = "view-main", Tabs = new[] { new EditorTabDefinition { Id = "bad", ContentId = "missing", Title = "Missing" } } } } };
        var cut = Render<DockLayoutComponent>(parameters => parameters.Add(component => component.Definition, definition));
        Assert.Contains("CONTENT_NOT_REGISTERED", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find(".xdl-layout"));
    }


    [Fact]
    public void PanelMoveMenuListsAllSixRegionsAndCommitsSelection()
    {
        PanelDragCommit? commit = null;
        var panel = new DockPanelInfo
        {
            Id = "explorer",
            Region = DockRegionName.LeftDockUpper,
            ContentId = "test",
            Title = "Explorer",
            Icon = "E",
            Visible = true,
            Active = true
        };

        var cut = Render<DockPanelView>(parameters => parameters
            .Add(component => component.Panel, panel)
            .Add(component => component.ContentRegistry, Services.GetRequiredService<IDockContentRegistry>())
            .Add(component => component.OnMove, request => commit = request));

        cut.Find(".xdl-panel-move-trigger").Click();
        Assert.Equal(6, cut.FindAll("[data-xdl-move-region]").Count);

        cut.Find("[data-xdl-move-region='RightDockLower']").Click();
        Assert.NotNull(commit);
        Assert.Equal("explorer", commit!.PanelId);
        Assert.Equal(DockRegionName.RightDockLower, commit.TargetRegion);
    }

    [Fact]
    public void ToolbarEntryRightClickOffersHideAction()
    {
        string? hiddenPanelId = null;
        var panel = new DockPanelInfo
        {
            Id = "explorer",
            Region = DockRegionName.LeftDockUpper,
            Title = "Explorer",
            Visible = true,
            Active = true
        };

        var cut = Render<DockToolBarEntry>(parameters => parameters
            .Add(component => component.Panel, panel)
            .Add(component => component.Side, "left")
            .Add(component => component.OnHide, panelId => hiddenPanelId = panelId));

        cut.Find("[data-xdl-toolbar-entry='explorer']")
            .TriggerEvent("oncontextmenu", new MouseEventArgs());
        cut.Find(".xdl-toolbar-context-menu [role='menuitem']").Click();

        Assert.Equal("explorer", hiddenPanelId);
    }

    [Fact]
    public void ClickingEditorTabActivatesTheSelectedTab()
    {
        var definition = new DockLayoutDefinition
        {
            EditorViews = new[]
            {
                new EditorViewDefinition
                {
                    Id = "view-main",
                    ActiveTabId = "tab-first",
                    Tabs = new[]
                    {
                        new EditorTabDefinition { Id = "tab-first", ContentId = "test", Title = "First" },
                        new EditorTabDefinition { Id = "tab-second", ContentId = "test", Title = "Second" }
                    }
                }
            }
        };

        var cut = Render<DockLayoutComponent>(parameters => parameters.Add(component => component.Definition, definition));
        cut.Find("[data-xdl-tab='tab-second'] .xdl-tab-activate").Click();

        cut.WaitForAssertion(() =>
            Assert.True(cut.Find("[data-xdl-tab='tab-second']").ClassList.Contains("xdl-active")));
    }

    [Fact]
    public void ExplicitLightThemeAddsTheLightThemeClass()
    {
        var definition = new DockLayoutDefinition
        {
            EditorViews = new[] { new EditorViewDefinition { Id = "view-main" } }
        };
        var options = new DockLayoutOptions { EnableThemeSupport = true, Theme = DockThemeMode.Light };
        var cut = Render<DockLayoutComponent>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.Options, options));

        Assert.True(cut.Find(".xdl-layout").ClassList.Contains("xdl-theme-light"));
    }

    [Fact]
    public void SplitterCollapseButtonsCanBeHiddenByOptions()
    {
        var definition = new DockLayoutDefinition
        {
            Panels = new[] { new DockPanelDefinition { Id = "panel", Region = DockRegionName.LeftDockUpper, ContentId = "test" } },
            EditorViews = new[] { new EditorViewDefinition { Id = "view-main" } }
        };
        var cut = Render<DockLayoutComponent>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.Options, new DockLayoutOptions { ShowSplitterCollapseButtons = false }));

        Assert.Empty(cut.FindAll(".xdl-splitter-collapse"));
    }

    [Fact]
    public void EditorTabContextMenuRaisesCloseAndSplitActions()
    {
        var actions = new List<EditorTabContextAction>();
        var tab = new EditorTabInfo
        {
            Id = "tab-a",
            ViewId = "view-main",
            ContentId = "test",
            Title = "A",
            Closable = true,
            Active = true
        };
        var cut = Render<EditorTabHeader>(parameters => parameters
            .Add(component => component.Tab, tab)
            .Add(component => component.ContentPanelId, "xdl-tabpanel-view-main")
            .Add(component => component.OnContextAction, request => actions.Add(request.Action)));

        cut.Find("[data-xdl-tab='tab-a']").TriggerEvent("oncontextmenu", new MouseEventArgs());
        cut.FindAll(".xdl-tab-context-menu button").Single(button => button.TextContent.Trim() == "Close").Click();
        Assert.Contains(EditorTabContextAction.Close, actions);

        cut.Find("[data-xdl-tab='tab-a']").TriggerEvent("oncontextmenu", new MouseEventArgs());
        cut.FindAll(".xdl-tab-context-menu button").Single(button => button.TextContent.Trim() == "Split and Move Right").Click();
        Assert.Contains(EditorTabContextAction.SplitAndMoveRight, actions);
    }

    [Fact]
    public void SplitterCollapseButtonInvokesConfiguredCallback()
    {
        var calls = 0;
        var cut = Render<DockSplitter>(parameters => parameters
            .Add(component => component.SplitterId, "split-a")
            .Add(component => component.Orientation, SplitOrientation.Vertical)
            .Add(component => component.ShowCollapseButton, true)
            .Add(component => component.CollapseEnabled, true)
            .Add(component => component.OnCollapse, () => calls++));

        cut.Find(".xdl-splitter-collapse").Click();
        Assert.Equal(1, calls);
    }

    [Fact]
    public void RepeatedActivationDoesNotRaiseDuplicateActivationEvents()
    {
        var activationCount = 0;
        var definition = new DockLayoutDefinition
        {
            EditorViews = new[]
            {
                new EditorViewDefinition
                {
                    Id = "view-main",
                    ActiveTabId = "tab-first",
                    Tabs = new[]
                    {
                        new EditorTabDefinition { Id = "tab-first", ContentId = "test", Title = "First" },
                        new EditorTabDefinition { Id = "tab-second", ContentId = "test", Title = "Second" }
                    }
                }
            }
        };

        var cut = Render<DockLayoutComponent>(parameters => parameters
            .Add(component => component.Definition, definition)
            .Add(component => component.OnTabActivated, _ => activationCount++));

        cut.Find("[data-xdl-tab='tab-second']").Click();
        cut.WaitForAssertion(() => Assert.Equal(1, activationCount));

        cut.Find("[data-xdl-tab='tab-second']").Click();
        cut.WaitForAssertion(() => Assert.Equal(1, activationCount));
    }

    [Fact]
    public void TabKeyboardNavigationRaisesTheTargetActivation()
    {
        string? activatedTabId = null;
        var tab = new EditorTabInfo
        {
            Id = "tab-middle",
            ViewId = "view-main",
            ContentId = "test",
            Title = "Middle",
            Active = true
        };

        var cut = Render<EditorTabHeader>(parameters => parameters
            .Add(component => component.Tab, tab)
            .Add(component => component.ContentPanelId, "tabpanel-main")
            .Add(component => component.PreviousTabId, "tab-first")
            .Add(component => component.NextTabId, "tab-last")
            .Add(component => component.FirstTabId, "tab-first")
            .Add(component => component.LastTabId, "tab-last")
            .Add(component => component.OnActivate, tabId => activatedTabId = tabId));

        cut.Find("[role='tab']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("tab-last", activatedTabId);

        cut.Find("[role='tab']").KeyDown(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal("tab-first", activatedTabId);
    }

    [Fact]
    public void TabStripConnectsTabsToTheControlledPanel()
    {
        var view = new EditorViewInfo
        {
            Id = "view-main",
            ActiveTabId = "tab-a",
            Tabs = new[]
            {
                new EditorTabInfo
                {
                    Id = "tab-a",
                    ViewId = "view-main",
                    ContentId = "test",
                    Title = "A",
                    Active = true
                }
            }
        };

        var cut = Render<EditorTabStrip>(parameters => parameters.Add(component => component.View, view));
        var tabElement = cut.Find("[role='tab']");
        Assert.Equal("xdl-tabpanel-view-main", tabElement.GetAttribute("aria-controls"));
        Assert.Equal("xdl-tab-tab-a", tabElement.Id);
    }

}
