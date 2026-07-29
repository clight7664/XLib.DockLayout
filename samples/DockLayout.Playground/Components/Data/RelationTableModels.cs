namespace DockLayout.Playground.Components.Data;

/// <summary>Describes one column rendered by the Playground relation table.</summary>
public sealed record RelationTableColumn
{
    /// <summary>Gets the dictionary key used to read the cell value.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the displayed column title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets an optional CSS width value.</summary>
    public string? Width { get; init; }
}

/// <summary>Represents one relationship record rendered by the Playground relation table.</summary>
public sealed record RelationTableRow
{
    /// <summary>Gets the stable row identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets values indexed by column key.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; init; } = new Dictionary<string, object?>();
}
