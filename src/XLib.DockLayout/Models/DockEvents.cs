namespace XLib.DockLayout.Models;

/// <summary>Base data supplied by DockLayout events.</summary>
public abstract record DockLayoutEventArgsBase
{
    /// <summary>Gets the source layout instance ID.</summary>
    public required string InstanceId { get; init; }
    /// <summary>Gets the operation reason.</summary>
    public DockLayoutChangeReason Reason { get; init; }
    /// <summary>Gets the current state revision.</summary>
    public long Revision { get; init; }
}

/// <summary>Data supplied when a DockLayout becomes ready.</summary>
public sealed record DockLayoutReadyEventArgs : DockLayoutEventArgsBase
{
    /// <summary>Gets the programmatic handle.</summary>
    public required Abstractions.IDockLayoutHandle Handle { get; init; }
}
/// <summary>Data supplied when layout state changes.</summary>
public sealed record DockLayoutChangedEventArgs : DockLayoutEventArgsBase
{
    /// <summary>Gets the changed object ID when available.</summary>
    public string? ObjectId { get; init; }
    /// <summary>Gets the current snapshot.</summary>
    public required DockLayoutSnapshot Snapshot { get; init; }
}
/// <summary>Data supplied for tab events.</summary>
public sealed record DockTabEventArgs : DockLayoutEventArgsBase
{
    /// <summary>Gets the affected tab.</summary>
    public required EditorTabInfo Tab { get; init; }
}
/// <summary>Data supplied for panel events.</summary>
public sealed record DockPanelEventArgs : DockLayoutEventArgsBase
{
    /// <summary>Gets the affected panel.</summary>
    public required DockPanelInfo Panel { get; init; }
}
/// <summary>Data supplied after a layout restore.</summary>
public sealed record DockLayoutRestoredEventArgs : DockLayoutEventArgsBase
{
    /// <summary>Gets the restore report.</summary>
    public required LayoutRestoreReport Report { get; init; }
}
