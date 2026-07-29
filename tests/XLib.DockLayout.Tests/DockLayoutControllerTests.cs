using XLib.DockLayout.Models;
using XLib.DockLayout.Services;

namespace XLib.DockLayout.Tests;

public sealed class DockLayoutControllerTests
{
    [Fact]
    public async Task OpenActivateCloseTabMaintainsLifecycle()
    {
        await using var controller = CreateController();
        var opened = await controller.OpenTabAsync(new OpenEditorTabRequest
        {
            ViewId = "view-main",
            ContentId = "test",
            Title = "A",
            DedupParameter = "/a",
            Parameters = new Dictionary<string, object?> { ["Value"] = "A" }
        });
        Assert.True(opened.Success);
        Assert.NotNull(opened.Value);

        var duplicate = await controller.OpenTabAsync(new OpenEditorTabRequest
        {
            ViewId = "view-main",
            ContentId = "test",
            Title = "A duplicate",
            DedupParameter = "/a"
        });
        Assert.True(duplicate.Success);
        Assert.Equal(opened.Value!.Id, duplicate.Value!.Id);

        var closed = await controller.CloseTabAsync(opened.Value.Id);
        Assert.True(closed.Success);
        var tabs = await controller.FindTabsAsync(new TabQuery { TabId = opened.Value.Id });
        Assert.Empty(tabs.Value!);
    }

    [Fact]
    public async Task PinnedTabsAreOrderedBeforeUnpinnedTabs()
    {
        await using var controller = CreateController();
        var first = await controller.OpenTabAsync(new OpenEditorTabRequest { ViewId = "view-main", ContentId = "test", Title = "First", DedupParameter = "1" });
        var second = await controller.OpenTabAsync(new OpenEditorTabRequest { ViewId = "view-main", ContentId = "test", Title = "Second", DedupParameter = "2" });
        Assert.True((await controller.SetTabPinnedAsync(second.Value!.Id, true)).Success);
        var snapshot = await controller.GetSnapshotAsync();
        Assert.Equal(second.Value.Id, snapshot.EditorViews.Single().Tabs.First().Id);
        Assert.Equal(first.Value!.Id, snapshot.EditorViews.Single().Tabs.Last().Id);
    }

    [Fact]
    public async Task MovePanelAcrossRegionsPreservesIdentity()
    {
        await using var controller = CreateController();
        Assert.True((await controller.MovePanelAsync("panel-a", DockRegionName.RightDockLower, 0)).Success);
        var snapshot = await controller.GetSnapshotAsync();
        var panel = Assert.Single(snapshot.Panels);
        Assert.Equal("panel-a", panel.Id);
        Assert.Equal(DockRegionName.RightDockLower, panel.Region);
        Assert.True(panel.Active);
    }

    [Fact]
    public async Task InstancesRemainIsolated()
    {
        await using var first = CreateController("first");
        await using var second = CreateController("second");
        await first.OpenTabAsync(new OpenEditorTabRequest { ViewId = "view-main", ContentId = "test", Title = "Only first", DedupParameter = "first" });
        Assert.Single((await first.GetSnapshotAsync()).EditorViews.Single().Tabs);
        Assert.Empty((await second.GetSnapshotAsync()).EditorViews.Single().Tabs);
    }


    [Fact]
    public async Task DirectionalDockCreatesBinarySplitAndMovesTab()
    {
        await using var controller = CreateController();
        var opened = await controller.OpenTabAsync(new OpenEditorTabRequest
        {
            ViewId = "view-main",
            ContentId = "test",
            Title = "Directional",
            DedupParameter = "directional"
        });
        Assert.True(opened.Success);
        var moved = await controller.MoveTabAsync(opened.Value!.Id, "view-main", DockDirection.Right);
        Assert.True(moved.Success);
        var snapshot = await controller.GetSnapshotAsync();
        var split = Assert.IsType<EditorSplitInfo>(snapshot.EditorRoot);
        Assert.Equal(SplitOrientation.Vertical, split.Orientation);
        Assert.Equal(2, snapshot.EditorViews.Count);
        Assert.Single(snapshot.EditorViews.Single(view => view.Id != "view-main").Tabs);
    }

    [Fact]
    public async Task SplitRatioCanBeChangedByStableSplitId()
    {
        await using var controller = CreateController();
        var splitResult = await controller.SplitEditorViewAsync("view-main", SplitOrientation.Horizontal);
        Assert.True(splitResult.Success);
        var before = await controller.GetSnapshotAsync();
        var split = Assert.IsType<EditorSplitInfo>(before.EditorRoot);
        Assert.True((await controller.ResizeEditorSplitAsync(split.Id, 0.7d)).Success);
        var after = await controller.GetSnapshotAsync();
        Assert.Equal(0.7d, Assert.IsType<EditorSplitInfo>(after.EditorRoot).FirstRatio, 6);
    }

    [Fact]
    public async Task AutoHideRemovesPanelFromOccupiedRegionButPreservesToolbarState()
    {
        await using var controller = CreateController();
        Assert.True((await controller.SetPanelAutoHideAsync("panel-a", true)).Success);
        var panel = Assert.Single((await controller.GetSnapshotAsync()).Panels);
        Assert.True(panel.AutoHide);
        Assert.True(panel.Collapsed);
        Assert.False(panel.Active);
    }



    [Fact]
    public async Task ToolbarEntryVisibilityIsIndependentFromPanelVisibility()
    {
        await using var controller = CreateController();
        Assert.True((await controller.SetToolBarEntryVisibilityAsync("panel-a", false)).Success);
        var hidden = Assert.Single((await controller.GetSnapshotAsync()).Panels);
        Assert.False(hidden.ToolBarVisible);
        Assert.True(hidden.Visible);
        Assert.True(hidden.Active);

        Assert.True((await controller.SetToolBarEntryVisibilityAsync("panel-a", true)).Success);
        var restored = Assert.Single((await controller.GetSnapshotAsync()).Panels);
        Assert.True(restored.ToolBarVisible);
        Assert.True(restored.Visible);
    }


    [Fact]
    public async Task ClosingLastTabInDynamicSplitPrunesTheSplitTree()
    {
        await using var controller = CreateController();
        var opened = await controller.OpenTabAsync(new OpenEditorTabRequest
        {
            ViewId = "view-main",
            ContentId = "test",
            Title = "Split",
            DedupParameter = "split-prune"
        });
        Assert.True(opened.Success);
        Assert.True((await controller.MoveTabAsync(opened.Value!.Id, "view-main", DockDirection.Right)).Success);

        var splitSnapshot = await controller.GetSnapshotAsync();
        Assert.IsType<EditorSplitInfo>(splitSnapshot.EditorRoot);
        var dynamicView = Assert.Single(splitSnapshot.EditorViews, view => view.Lifecycle == EditorViewLifecycle.Dynamic);
        Assert.Equal(opened.Value.Id, Assert.Single(dynamicView.Tabs).Id);

        Assert.True((await controller.CloseTabAsync(opened.Value.Id)).Success);
        var pruned = await controller.GetSnapshotAsync();
        Assert.Single(pruned.EditorViews);
        var leaf = Assert.IsType<EditorLeafInfo>(pruned.EditorRoot);
        Assert.Equal("view-main", leaf.ViewId);
    }

    [Fact]
    public async Task EditorSplitterCollapseTogglesWithoutLosingItsRatio()
    {
        await using var controller = CreateController();
        var splitResult = await controller.SplitEditorViewAsync("view-main", SplitOrientation.Vertical);
        Assert.True(splitResult.Success);
        var before = Assert.IsType<EditorSplitInfo>((await controller.GetSnapshotAsync()).EditorRoot);
        var ratio = before.FirstRatio;

        Assert.True((await controller.ToggleEditorSplitCollapseAsync(before.Id)).Success);
        var collapsed = Assert.IsType<EditorSplitInfo>((await controller.GetSnapshotAsync()).EditorRoot);
        Assert.Equal(SplitCollapsedSide.Second, collapsed.CollapsedSide);
        Assert.Equal(ratio, collapsed.FirstRatio, 6);

        Assert.True((await controller.ToggleEditorSplitCollapseAsync(before.Id)).Success);
        var expanded = Assert.IsType<EditorSplitInfo>((await controller.GetSnapshotAsync()).EditorRoot);
        Assert.Equal(SplitCollapsedSide.None, expanded.CollapsedSide);
        Assert.Equal(ratio, expanded.FirstRatio, 6);
    }

    [Fact]
    public async Task LayoutSplitterCollapseTogglesTheSelectedDockOnly()
    {
        await using var controller = CreateController();
        Assert.True((await controller.ToggleLayoutSplitterCollapseAsync("layout-left-editor")).Success);
        var collapsed = await controller.GetSnapshotAsync();
        Assert.True(collapsed.Geometry.LeftDockCollapsed);
        Assert.False(collapsed.Geometry.RightDockCollapsed);

        Assert.True((await controller.ToggleLayoutSplitterCollapseAsync("layout-left-editor")).Success);
        Assert.False((await controller.GetSnapshotAsync()).Geometry.LeftDockCollapsed);
    }


    [Fact]
    public async Task ClosingTabsInNestedDynamicSplitsRecursivelyCompressesTheTree()
    {
        await using var controller = CreateController();
        var rightTab = await controller.OpenTabAsync(new OpenEditorTabRequest
        {
            ViewId = "view-main",
            ContentId = "test",
            Title = "Right",
            DedupParameter = "nested-right"
        });
        Assert.True(rightTab.Success);
        Assert.True((await controller.MoveTabAsync(rightTab.Value!.Id, "view-main", DockDirection.Right)).Success);

        var bottomTab = await controller.OpenTabAsync(new OpenEditorTabRequest
        {
            ViewId = "view-main",
            ContentId = "test",
            Title = "Bottom",
            DedupParameter = "nested-bottom"
        });
        Assert.True(bottomTab.Success);
        Assert.True((await controller.MoveTabAsync(bottomTab.Value!.Id, "view-main", DockDirection.Bottom)).Success);

        var nested = await controller.GetSnapshotAsync();
        Assert.Equal(3, nested.EditorViews.Count);
        Assert.IsType<EditorSplitInfo>(nested.EditorRoot);

        Assert.True((await controller.CloseTabAsync(bottomTab.Value.Id)).Success);
        var afterBottom = await controller.GetSnapshotAsync();
        Assert.Equal(2, afterBottom.EditorViews.Count);
        Assert.IsType<EditorSplitInfo>(afterBottom.EditorRoot);

        Assert.True((await controller.CloseTabAsync(rightTab.Value.Id)).Success);
        var compressed = await controller.GetSnapshotAsync();
        Assert.Single(compressed.EditorViews);
        Assert.Equal("view-main", Assert.IsType<EditorLeafInfo>(compressed.EditorRoot).ViewId);
    }

    [Fact]
    public async Task ReorderingWithinTheSameViewKeepsMovedTabActive()
    {
        await using var controller = CreateController();
        var first = await controller.OpenTabAsync(new OpenEditorTabRequest
        {
            ViewId = "view-main",
            ContentId = "test",
            Title = "First",
            DedupParameter = "reorder-first"
        });
        var second = await controller.OpenTabAsync(new OpenEditorTabRequest
        {
            ViewId = "view-main",
            ContentId = "test",
            Title = "Second",
            DedupParameter = "reorder-second"
        });
        Assert.True(first.Success && second.Success);

        Assert.True((await controller.MoveTabAsync(second.Value!.Id, "view-main", DockDirection.Center, 0)).Success);
        var view = Assert.Single((await controller.GetSnapshotAsync()).EditorViews);
        Assert.Equal(second.Value.Id, view.ActiveTabId);
        Assert.Equal(second.Value.Id, view.Tabs[0].Id);
    }

    [Fact]
    public async Task MovingCollapsedPanelToAnotherRegionExpandsAndActivatesIt()
    {
        await using var controller = CreateController();
        Assert.True((await controller.CollapsePanelAsync("panel-a")).Success);
        Assert.True((await controller.MovePanelAsync("panel-a", DockRegionName.BottomRightDock, 0)).Success);

        var moved = Assert.Single((await controller.GetSnapshotAsync()).Panels);
        Assert.Equal(DockRegionName.BottomRightDock, moved.Region);
        Assert.True(moved.Visible);
        Assert.False(moved.Collapsed);
        Assert.False(moved.AutoHide);
        Assert.True(moved.Active);
    }

    [Fact]
    public async Task ActivatingTheAlreadyActiveTabDoesNotAdvanceTheRevision()
    {
        await using var controller = CreateController();
        var opened = await controller.OpenTabAsync(new OpenEditorTabRequest
        {
            ViewId = "view-main",
            ContentId = "test",
            Title = "Active",
            DedupParameter = "active-no-op"
        });
        Assert.True(opened.Success);

        var before = await controller.GetSnapshotAsync();
        var result = await controller.ActivateTabAsync(opened.Value!.Id);
        var after = await controller.GetSnapshotAsync();

        Assert.True(result.Success);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(opened.Value.Id, Assert.Single(after.EditorViews).ActiveTabId);
    }

    private static DockLayoutController CreateController(string id = "test")
    {
        var registry = new DockContentRegistry();
        Assert.True(registry.Register<TestContent>("test").Success);
        return new DockLayoutController(id, new DockLayoutDefinition
        {
            Panels = new[] { new DockPanelDefinition { Id = "panel-a", Region = DockRegionName.LeftDockUpper, ContentId = "test", Title = "Panel A" } },
            EditorViews = new[] { new EditorViewDefinition { Id = "view-main", Lifecycle = EditorViewLifecycle.Template } }
        }, new DockLayoutOptions { AutoSave = false }, registry, new DockLayoutSerializer(), new MemoryPersistence());
    }
}
