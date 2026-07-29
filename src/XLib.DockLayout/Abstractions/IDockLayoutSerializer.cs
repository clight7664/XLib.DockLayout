using XLib.DockLayout.Models;

namespace XLib.DockLayout.Abstractions;

/// <summary>Serializes and validates versioned XML layout data.</summary>
public interface IDockLayoutSerializer
{
    /// <summary>Serializes a snapshot to XML.</summary>
    DockLayoutResult<string> Serialize(DockLayoutSnapshot snapshot);
    /// <summary>Deserializes XML to a validated snapshot.</summary>
    DockLayoutResult<DockLayoutSnapshot> Deserialize(string xml);
}
