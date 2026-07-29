namespace XLib.DockLayout.Abstractions;

/// <summary>Stores serialized layouts and named presets.</summary>
public interface IDockLayoutPersistence
{
    /// <summary>Saves XML for a key.</summary>
    ValueTask SaveAsync(string key, string xml, CancellationToken cancellationToken = default);
    /// <summary>Loads XML for a key.</summary>
    ValueTask<string?> LoadAsync(string key, CancellationToken cancellationToken = default);
    /// <summary>Deletes a key.</summary>
    ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default);
}
