using XLib.DockLayout.Models;
using XLib.DockLayout.Services;

namespace XLib.DockLayout.Tests;

public sealed class SerializationTests
{
    private readonly DockLayoutSerializer _serializer = new();

    [Fact]
    public void XmlRoundTripPreservesPanelViewAndTab()
    {
        var snapshot = CreateSnapshot();
        var xml = _serializer.Serialize(snapshot);
        Assert.True(xml.Success);
        Assert.Contains("<XLibDockLayout", xml.Value, StringComparison.Ordinal);
        var restored = _serializer.Deserialize(xml.Value!);
        Assert.True(restored.Success);
        Assert.Equal(snapshot.Panels.Single().Id, restored.Value!.Panels.Single().Id);
        Assert.Equal(snapshot.Panels.Single().ToolBarVisible, restored.Value.Panels.Single().ToolBarVisible);
        Assert.Equal(snapshot.EditorViews.Single().Tabs.Single().Id, restored.Value.EditorViews.Single().Tabs.Single().Id);
    }

    [Fact]
    public void IncompatibleMajorVersionIsRejected()
    {
        var result = _serializer.Deserialize("<XLibDockLayout version=\"2.0.0\"><EditorArea /></XLibDockLayout>");
        Assert.False(result.Success);
        Assert.Equal("LAYOUT_VERSION_INCOMPATIBLE", result.Error?.Code);
    }

    [Fact]
    public void DtdAndExternalEntityAreRejected()
    {
        const string xml = "<!DOCTYPE x [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]><XLibDockLayout version=\"1.0.0\"><EditorArea><View nodeId=\"leaf-v\" id=\"v\"><Tab id=\"t\" contentId=\"test\" title=\"&xxe;\" /></View></EditorArea></XLibDockLayout>";
        var result = _serializer.Deserialize(xml);
        Assert.False(result.Success);
        Assert.Equal("INVALID_LAYOUT_DATA", result.Error?.Code);
    }

    [Fact]
    public async Task FailedImportDoesNotMutateControllerState()
    {
        var registry = new DockContentRegistry();
        registry.Register<TestContent>("test");
        using var cancellation = new CancellationTokenSource();
        await using var controller = new DockLayoutController("atomic", new DockLayoutDefinition { EditorViews = new[] { new EditorViewDefinition { Id = "view-main" } } }, new DockLayoutOptions { AutoSave = false }, registry, _serializer, new MemoryPersistence());
        var before = await controller.GetSnapshotAsync(cancellation.Token);
        var result = await controller.ImportLayoutXmlAsync("<broken", cancellation.Token);
        var after = await controller.GetSnapshotAsync(cancellation.Token);
        Assert.False(result.Success);
        Assert.Equal(before.Revision, after.Revision);
    }


    [Fact]
    public void NestedEditorTreeRoundTripPreservesSplitIdentityAndRatio()
    {
        var first = CreateSnapshot();
        var secondView = new EditorViewInfo
        {
            Id = "view-second",
            Lifecycle = EditorViewLifecycle.Dynamic,
            Ratio = 1d,
            Orientation = SplitOrientation.Vertical,
            Order = 1
        };
        var snapshot = first with
        {
            EditorViews = first.EditorViews.Concat(new[] { secondView }).ToArray(),
            EditorRoot = new EditorSplitInfo
            {
                Id = "split-root",
                Orientation = SplitOrientation.Vertical,
                FirstRatio = 0.65d,
                CollapsedSide = SplitCollapsedSide.Second,
                First = first.EditorRoot,
                Second = new EditorLeafInfo { Id = "leaf-view-second", ViewId = "view-second" }
            }
        };
        var xml = _serializer.Serialize(snapshot);
        Assert.True(xml.Success);
        Assert.Contains("<Split", xml.Value, StringComparison.Ordinal);
        var restored = _serializer.Deserialize(xml.Value!);
        Assert.True(restored.Success);
        var restoredSplit = Assert.IsType<EditorSplitInfo>(restored.Value!.EditorRoot);
        Assert.Equal("split-root", restoredSplit.Id);
        Assert.Equal(0.65d, restoredSplit.FirstRatio, 6);
        Assert.Equal(SplitCollapsedSide.Second, restoredSplit.CollapsedSide);
    }

    private static DockLayoutSnapshot CreateSnapshot() => new()
    {
        Version = "1.0.0",
        InstanceId = "roundtrip",
        Revision = 3,
        Panels = new[] { new DockPanelInfo { Id = "explorer", Region = DockRegionName.LeftDockUpper, ContentId = "test", Title = "Explorer", Lifecycle = DockPanelLifecycle.Template, Ratio = 1, MinSize = 100, MaxSize = 500, Visible = true, ToolBarVisible = false, Active = true } },
        EditorViews = new[] { new EditorViewInfo { Id = "view-main", Lifecycle = EditorViewLifecycle.Template, ActiveTabId = "tab-a", Ratio = 1, Orientation = SplitOrientation.Vertical, Tabs = new[] { new EditorTabInfo { Id = "tab-a", ViewId = "view-main", ContentId = "test", Title = "A", Closable = true, Active = true, Parameters = new Dictionary<string, object?> { ["Value"] = "A" } } } } },
        EditorRoot = new EditorLeafInfo { Id = "leaf-view-main", ViewId = "view-main" }
    };
}
