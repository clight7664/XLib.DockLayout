namespace XLib.DockLayout.Models;

/// <summary>Base type for a declarative EditorArea split tree node.</summary>
public abstract record EditorNodeDefinition
{
    /// <summary>Gets the globally unique node identifier.</summary>
    public required string Id { get; init; }
}

/// <summary>References an EditorView from a declarative split tree leaf.</summary>
public sealed record EditorLeafDefinition : EditorNodeDefinition
{
    /// <summary>Gets the referenced EditorView identifier.</summary>
    public required string ViewId { get; init; }
}

/// <summary>Defines a binary EditorArea split node.</summary>
public sealed record EditorSplitDefinition : EditorNodeDefinition
{
    /// <summary>Gets the split orientation.</summary>
    public SplitOrientation Orientation { get; init; }

    /// <summary>Gets the normalized size assigned to the first child.</summary>
    public double FirstRatio { get; init; } = 0.5d;

    /// <summary>Gets the currently collapsed child.</summary>
    public SplitCollapsedSide CollapsedSide { get; init; }

    /// <summary>Gets the first child node.</summary>
    public required EditorNodeDefinition First { get; init; }

    /// <summary>Gets the second child node.</summary>
    public required EditorNodeDefinition Second { get; init; }
}

/// <summary>Base type for an immutable EditorArea split tree snapshot node.</summary>
public abstract record EditorNodeInfo
{
    /// <summary>Gets the globally unique node identifier.</summary>
    public required string Id { get; init; }
}

/// <summary>References an EditorView from an immutable split tree leaf.</summary>
public sealed record EditorLeafInfo : EditorNodeInfo
{
    /// <summary>Gets the referenced EditorView identifier.</summary>
    public required string ViewId { get; init; }
}

/// <summary>Provides an immutable binary EditorArea split node.</summary>
public sealed record EditorSplitInfo : EditorNodeInfo
{
    /// <summary>Gets the split orientation.</summary>
    public SplitOrientation Orientation { get; init; }

    /// <summary>Gets the normalized size assigned to the first child.</summary>
    public double FirstRatio { get; init; } = 0.5d;

    /// <summary>Gets the currently collapsed child.</summary>
    public SplitCollapsedSide CollapsedSide { get; init; }

    /// <summary>Gets the first child node.</summary>
    public required EditorNodeInfo First { get; init; }

    /// <summary>Gets the second child node.</summary>
    public required EditorNodeInfo Second { get; init; }
}

/// <summary>Describes a browser tab drag commit.</summary>
public sealed record TabDragCommit
{
    /// <summary>Gets the dragged tab identifier.</summary>
    public required string TabId { get; init; }

    /// <summary>Gets the source EditorView identifier.</summary>
    public required string SourceViewId { get; init; }

    /// <summary>Gets the target EditorView identifier.</summary>
    public required string TargetViewId { get; init; }

    /// <summary>Gets the target docking direction.</summary>
    public DockDirection Direction { get; init; }

    /// <summary>Gets the target tab index for center docking.</summary>
    public int TargetIndex { get; init; } = -1;
}

/// <summary>Describes a browser DockPanel drag commit.</summary>
public sealed record PanelDragCommit
{
    /// <summary>Gets the dragged panel identifier.</summary>
    public required string PanelId { get; init; }

    /// <summary>Gets the target Dock Region.</summary>
    public DockRegionName TargetRegion { get; init; }

    /// <summary>Gets the target order.</summary>
    public int TargetIndex { get; init; } = -1;
}

/// <summary>Describes a request to resize an EditorArea split node.</summary>
public sealed record EditorSplitResizeRequest
{
    /// <summary>Gets the split node identifier.</summary>
    public required string SplitId { get; init; }

    /// <summary>Gets the normalized first-child ratio.</summary>
    public double FirstRatio { get; init; }
}

/// <summary>Describes an EditorTab context-menu command.</summary>
public sealed record EditorTabContextActionRequest
{
    /// <summary>Gets the selected tab identifier.</summary>
    public required string TabId { get; init; }
    /// <summary>Gets the owner EditorView identifier.</summary>
    public required string ViewId { get; init; }
    /// <summary>Gets the requested context action.</summary>
    public EditorTabContextAction Action { get; init; }
}

/// <summary>Describes a splitter collapse/expand request.</summary>
public sealed record SplitterCollapseRequest
{
    /// <summary>Gets the stable splitter identifier.</summary>
    public required string SplitterId { get; init; }
}
