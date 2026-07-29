namespace XLib.DockLayout.Models;

/// <summary>Provides a read-only DockPanel snapshot.</summary>
public sealed record DockPanelInfo
{
    /// <summary>Gets the panel ID.</summary>
    public required string Id { get; init; }
    /// <summary>Gets the current region.</summary>
    public DockRegionName Region { get; init; }
    /// <summary>Gets the content ID.</summary>
    public string? ContentId { get; init; }
    /// <summary>Gets the title.</summary>
    public required string Title { get; init; }
    /// <summary>Gets the icon.</summary>
    public string? Icon { get; init; }
    /// <summary>Gets the lifecycle.</summary>
    public DockPanelLifecycle Lifecycle { get; init; }
    /// <summary>Gets the current ratio.</summary>
    public double Ratio { get; init; }
    /// <summary>Gets the minimum size.</summary>
    public double MinSize { get; init; }
    /// <summary>Gets the maximum size.</summary>
    public double MaxSize { get; init; }
    /// <summary>Gets whether the panel is visible.</summary>
    public bool Visible { get; init; }
    /// <summary>Gets whether the panel is represented by an LT/RT toolbar entry.</summary>
    public bool ToolBarVisible { get; init; } = true;
    /// <summary>Gets whether the panel is collapsed.</summary>
    public bool Collapsed { get; init; }
    /// <summary>Gets whether the panel is active in its region.</summary>
    public bool Active { get; init; }
    /// <summary>Gets whether the panel uses auto-hide.</summary>
    public bool AutoHide { get; init; }
    /// <summary>Gets the order within the region.</summary>
    public int Order { get; init; }
    /// <summary>Gets content parameters.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
}

/// <summary>Provides a read-only EditorTab snapshot.</summary>
public sealed record EditorTabInfo
{
    /// <summary>Gets the tab ID.</summary>
    public required string Id { get; init; }
    /// <summary>Gets the owner view ID.</summary>
    public required string ViewId { get; init; }
    /// <summary>Gets the content ID.</summary>
    public required string ContentId { get; init; }
    /// <summary>Gets the visible title.</summary>
    public required string Title { get; init; }
    /// <summary>Gets the icon.</summary>
    public string? Icon { get; init; }
    /// <summary>Gets whether the tab is closable.</summary>
    public bool Closable { get; init; }
    /// <summary>Gets whether the tab is pinned.</summary>
    public bool Pinned { get; init; }
    /// <summary>Gets whether the tab is active.</summary>
    public bool Active { get; init; }
    /// <summary>Gets the order within the view.</summary>
    public int Order { get; init; }
    /// <summary>Gets the optional deduplication parameter.</summary>
    public string? DedupParameter { get; init; }
    /// <summary>Gets content parameters.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
}

/// <summary>Provides a read-only EditorView snapshot.</summary>
public sealed record EditorViewInfo
{
    /// <summary>Gets the view ID.</summary>
    public required string Id { get; init; }
    /// <summary>Gets the lifecycle.</summary>
    public EditorViewLifecycle Lifecycle { get; init; }
    /// <summary>Gets the active tab ID.</summary>
    public string? ActiveTabId { get; init; }
    /// <summary>Gets the normalized ratio.</summary>
    public double Ratio { get; init; }
    /// <summary>Gets the split orientation.</summary>
    public SplitOrientation Orientation { get; init; }
    /// <summary>Gets the order in the editor area.</summary>
    public int Order { get; init; }
    /// <summary>Gets tabs in display order.</summary>
    public IReadOnlyList<EditorTabInfo> Tabs { get; init; } = Array.Empty<EditorTabInfo>();
}

/// <summary>Provides an immutable layout snapshot.</summary>
public sealed record DockLayoutSnapshot
{
    /// <summary>Gets the XML/layout version.</summary>
    public required string Version { get; init; }
    /// <summary>Gets the layout instance ID.</summary>
    public required string InstanceId { get; init; }
    /// <summary>Gets the state revision.</summary>
    public long Revision { get; init; }
    /// <summary>Gets DockPanels.</summary>
    public IReadOnlyList<DockPanelInfo> Panels { get; init; } = Array.Empty<DockPanelInfo>();
    /// <summary>Gets EditorViews.</summary>
    public IReadOnlyList<EditorViewInfo> EditorViews { get; init; } = Array.Empty<EditorViewInfo>();
    /// <summary>Gets the immutable EditorArea binary split tree.</summary>
    public required EditorNodeInfo EditorRoot { get; init; }
    /// <summary>Gets normalized top-level layout geometry.</summary>
    public DockLayoutGeometry Geometry { get; init; } = new();
}
