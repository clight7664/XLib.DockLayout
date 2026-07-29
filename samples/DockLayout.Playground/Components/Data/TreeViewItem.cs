namespace DockLayout.Playground.Components.Data;

/// <summary>Represents one immutable item rendered by the Playground TreeView component.</summary>
public sealed record TreeViewItem
{
    /// <summary>Gets the stable item identifier.</summary>
    public required string Id { get; init; }
    /// <summary>Gets the visible node label.</summary>
    public required string Label { get; init; }
    /// <summary>Gets an optional compact icon.</summary>
    public string? Icon { get; init; }
    /// <summary>Gets whether the item is expanded initially.</summary>
    public bool Expanded { get; init; }
    /// <summary>Gets an optional path opened when the node is activated.</summary>
    public string? OpenPath { get; init; }
    /// <summary>Gets an optional registered content identifier used when opening the node.</summary>
    public string? ContentId { get; init; }
    /// <summary>Gets optional editor text used by the Playground sample.</summary>
    public string? EditorText { get; init; }
    /// <summary>Gets child items.</summary>
    public IReadOnlyList<TreeViewItem> Children { get; init; } = Array.Empty<TreeViewItem>();
}
