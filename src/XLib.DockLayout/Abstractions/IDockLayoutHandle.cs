using XLib.DockLayout.Models;

namespace XLib.DockLayout.Abstractions;

/// <summary>Provides serialized, instance-scoped DockLayout operations.</summary>
public interface IDockLayoutHandle : IAsyncDisposable
{
    /// <summary>Gets the unique layout instance ID.</summary>
    string InstanceId { get; }
    /// <summary>Opens or activates an EditorTab.</summary>
    ValueTask<DockLayoutResult<EditorTabInfo>> OpenTabAsync(OpenEditorTabRequest request, CancellationToken cancellationToken = default);
    /// <summary>Closes an EditorTab.</summary>
    ValueTask<DockLayoutResult> CloseTabAsync(string tabId, CancellationToken cancellationToken = default);
    /// <summary>Activates an EditorTab.</summary>
    ValueTask<DockLayoutResult> ActivateTabAsync(string tabId, CancellationToken cancellationToken = default);
    /// <summary>Finds EditorTabs.</summary>
    ValueTask<DockLayoutResult<IReadOnlyList<EditorTabInfo>>> FindTabsAsync(TabQuery query, CancellationToken cancellationToken = default);
    /// <summary>Pins or unpins an EditorTab.</summary>
    ValueTask<DockLayoutResult> SetTabPinnedAsync(string tabId, bool pinned, CancellationToken cancellationToken = default);
    /// <summary>Moves an EditorTab to a target view or directional split.</summary>
    ValueTask<DockLayoutResult> MoveTabAsync(string tabId, string targetViewId, DockDirection direction, int targetIndex = -1, CancellationToken cancellationToken = default);
    /// <summary>Splits an EditorView.</summary>
    ValueTask<DockLayoutResult<string>> SplitEditorViewAsync(string viewId, SplitOrientation orientation, CancellationToken cancellationToken = default);
    /// <summary>Resizes a binary EditorArea split node by normalized first-child ratio.</summary>
    ValueTask<DockLayoutResult> ResizeEditorSplitAsync(string splitId, double firstRatio, CancellationToken cancellationToken = default);
    /// <summary>Toggles collapse/expand for the second child of an EditorArea split.</summary>
    ValueTask<DockLayoutResult> ToggleEditorSplitCollapseAsync(string splitId, CancellationToken cancellationToken = default);
    /// <summary>Resizes a top-level Dock Region splitter by normalized ratio.</summary>
    ValueTask<DockLayoutResult> ResizeLayoutSplitterAsync(string splitterId, double ratio, CancellationToken cancellationToken = default);
    /// <summary>Toggles collapse/expand for a top-level Dock Region splitter.</summary>
    ValueTask<DockLayoutResult> ToggleLayoutSplitterCollapseAsync(string splitterId, CancellationToken cancellationToken = default);
    /// <summary>Collapses a DockPanel.</summary>
    ValueTask<DockLayoutResult> CollapsePanelAsync(string panelId, CancellationToken cancellationToken = default);
    /// <summary>Expands and activates a DockPanel.</summary>
    ValueTask<DockLayoutResult> ExpandPanelAsync(string panelId, CancellationToken cancellationToken = default);
    /// <summary>Sets DockPanel visibility.</summary>
    ValueTask<DockLayoutResult> SetPanelVisibilityAsync(string panelId, bool visible, CancellationToken cancellationToken = default);
    /// <summary>Shows or hides the DockPanel LT/RT toolbar entry without changing panel visibility.</summary>
    ValueTask<DockLayoutResult> SetToolBarEntryVisibilityAsync(string panelId, bool visible, CancellationToken cancellationToken = default);
    /// <summary>Enables or disables DockPanel auto-hide mode.</summary>
    ValueTask<DockLayoutResult> SetPanelAutoHideAsync(string panelId, bool autoHide, CancellationToken cancellationToken = default);
    /// <summary>Moves a DockPanel to a Dock Region and index.</summary>
    ValueTask<DockLayoutResult> MovePanelAsync(string panelId, DockRegionName target, int targetIndex, CancellationToken cancellationToken = default);
    /// <summary>Exports the current layout XML.</summary>
    ValueTask<DockLayoutResult<string>> ExportLayoutXmlAsync(CancellationToken cancellationToken = default);
    /// <summary>Atomically imports layout XML.</summary>
    ValueTask<DockLayoutResult<LayoutRestoreReport>> ImportLayoutXmlAsync(string xml, CancellationToken cancellationToken = default);
    /// <summary>Saves the current layout as a named preset.</summary>
    ValueTask<DockLayoutResult> SavePresetAsync(string name, CancellationToken cancellationToken = default);
    /// <summary>Restores a named preset.</summary>
    ValueTask<DockLayoutResult> RestorePresetAsync(string name, CancellationToken cancellationToken = default);
    /// <summary>Deletes a named preset.</summary>
    ValueTask<DockLayoutResult> DeletePresetAsync(string name, CancellationToken cancellationToken = default);
    /// <summary>Returns the immutable current snapshot.</summary>
    ValueTask<DockLayoutSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
