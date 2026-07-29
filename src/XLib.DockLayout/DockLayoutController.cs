using XLib.DockLayout.Abstractions;
using XLib.DockLayout.Models;
using XLib.DockLayout.State;

namespace XLib.DockLayout;

/// <summary>Default instance-scoped implementation of <see cref="IDockLayoutHandle"/>.</summary>
public sealed class DockLayoutController : IDockLayoutHandle
{
    private readonly DockLayoutStore _store;
    private readonly DockLayoutCommandQueue _queue = new();
    private readonly IDockContentRegistry _registry;
    private readonly IDockLayoutSerializer _serializer;
    private readonly IDockLayoutPersistence _persistence;
    private readonly DockLayoutOptions _options;
    private bool _disposed;
    private CancellationTokenSource? _saveDebounceCancellation;

    /// <summary>Creates a controller from a declarative definition.</summary>
    public DockLayoutController(
        string instanceId,
        DockLayoutDefinition definition,
        DockLayoutOptions options,
        IDockContentRegistry registry,
        IDockLayoutSerializer serializer,
        IDockLayoutPersistence persistence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(persistence);
        InstanceId = instanceId;
        _options = options;
        _registry = registry;
        _serializer = serializer;
        _persistence = persistence;
        _store = new DockLayoutStore(instanceId, definition);
        _store.Changed += HandleStoreChanged;
    }

    /// <inheritdoc />
    public string InstanceId { get; }

    internal event EventHandler<DockLayoutChangedEventArgs>? LayoutChanged;
    internal event EventHandler<DockTabEventArgs>? TabOpened;
    internal event EventHandler<DockTabEventArgs>? TabActivated;
    internal event EventHandler<DockTabEventArgs>? TabClosed;
    internal event EventHandler<DockPanelEventArgs>? PanelCreated;
    internal event EventHandler<DockPanelEventArgs>? PanelRemoved;
    internal event EventHandler<DockLayoutRestoredEventArgs>? LayoutRestored;

    /// <inheritdoc />
    public ValueTask<DockLayoutResult<EditorTabInfo>> OpenTabAsync(OpenEditorTabRequest request, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct =>
        {
            if (!_registry.TryResolve(request.ContentId, out _))
            {
                return DockLayoutResult<EditorTabInfo>.Fail("CONTENT_NOT_REGISTERED", $"Content ID '{request.ContentId}' is not registered.");
            }

            var before = _store.FindTabs(new TabQuery
            {
                ViewId = request.ViewId,
                ContentId = request.ContentId,
                DedupParameter = request.DedupParameter
            });
            var previouslyActiveId = _store.GetSnapshot().EditorViews
                .FirstOrDefault(view => view.Id == request.ViewId)?.ActiveTabId;

            var result = _store.OpenTab(request, _options.TabDedupMode);
            if (result.Success && result.Value is not null)
            {
                var existed = before.Any(tab => tab.Id == result.Value.Id);
                if (!existed)
                {
                    TabOpened?.Invoke(this, TabArgs(result.Value, DockLayoutChangeReason.Api));
                }

                if (!string.Equals(previouslyActiveId, result.Value.Id, StringComparison.Ordinal))
                {
                    TabActivated?.Invoke(this, TabArgs(result.Value, DockLayoutChangeReason.Api));
                }
            }
            return result;
        }, cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> CloseTabAsync(string tabId, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct =>
        {
            var before = _store.FindTabs(new TabQuery { TabId = tabId }).FirstOrDefault();
            var result = _store.CloseTab(tabId, _options.UseRecentAccessActivation);
            if (result.Success && before is not null) TabClosed?.Invoke(this, TabArgs(before, DockLayoutChangeReason.Api));
            return result;
        }, cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> ActivateTabAsync(string tabId, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct =>
        {
            var before = _store.FindTabs(new TabQuery { TabId = tabId }).FirstOrDefault();
            var wasActive = before?.Active == true;
            var result = _store.ActivateTab(tabId);
            var tab = _store.FindTabs(new TabQuery { TabId = tabId }).FirstOrDefault();
            if (result.Success && tab is not null && !wasActive)
            {
                TabActivated?.Invoke(this, TabArgs(tab, DockLayoutChangeReason.Api));
            }
            return result;
        }, cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult<IReadOnlyList<EditorTabInfo>>> FindTabsAsync(TabQuery query, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => DockLayoutResult<IReadOnlyList<EditorTabInfo>>.Ok(_store.FindTabs(query)), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> SetTabPinnedAsync(string tabId, bool pinned, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.SetTabPinned(tabId, pinned), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> MoveTabAsync(string tabId, string targetViewId, DockDirection direction, int targetIndex = -1, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.MoveTab(tabId, targetViewId, direction, targetIndex), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult<string>> SplitEditorViewAsync(string viewId, SplitOrientation orientation, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.SplitView(viewId, orientation), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> ResizeEditorSplitAsync(string splitId, double firstRatio, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.ResizeEditorSplit(splitId, firstRatio), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> ToggleEditorSplitCollapseAsync(string splitId, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.ToggleEditorSplitCollapse(splitId), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> ResizeLayoutSplitterAsync(string splitterId, double ratio, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.ResizeLayoutSplitter(splitterId, ratio), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> ToggleLayoutSplitterCollapseAsync(string splitterId, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.ToggleLayoutSplitterCollapse(splitterId), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> CollapsePanelAsync(string panelId, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.CollapsePanel(panelId), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> ExpandPanelAsync(string panelId, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.ExpandPanel(panelId), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> SetPanelVisibilityAsync(string panelId, bool visible, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.SetPanelVisibility(panelId, visible), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> SetToolBarEntryVisibilityAsync(string panelId, bool visible, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.SetToolBarEntryVisibility(panelId, visible), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> SetPanelAutoHideAsync(string panelId, bool autoHide, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.SetPanelAutoHide(panelId, autoHide), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult> MovePanelAsync(string panelId, DockRegionName target, int targetIndex, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.MovePanel(panelId, target, targetIndex), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult<string>> ExportLayoutXmlAsync(CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _serializer.Serialize(_store.GetSnapshot()), cancellationToken);

    /// <inheritdoc />
    public ValueTask<DockLayoutResult<LayoutRestoreReport>> ImportLayoutXmlAsync(string xml, CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct =>
        {
            var parsed = _serializer.Deserialize(xml);
            if (!parsed.Success || parsed.Value is null) return DockLayoutResult<LayoutRestoreReport>.Fail(parsed.Error!.Code, parsed.Error.Message);
            var snapshot = parsed.Value with { InstanceId = InstanceId };
            var replacement = _store.Replace(snapshot, DockLayoutChangeReason.Import);
            if (!replacement.Success) return DockLayoutResult<LayoutRestoreReport>.Fail(replacement.Error!.Code, replacement.Error.Message);
            var report = new LayoutRestoreReport
            {
                Version = snapshot.Version,
                PanelCount = snapshot.Panels.Count,
                ViewCount = snapshot.EditorViews.Count,
                TabCount = snapshot.EditorViews.Sum(view => view.Tabs.Count)
            };
            LayoutRestored?.Invoke(this, new DockLayoutRestoredEventArgs
            {
                InstanceId = InstanceId,
                Reason = DockLayoutChangeReason.Import,
                Revision = _store.GetSnapshot().Revision,
                Report = report
            });
            return DockLayoutResult<LayoutRestoreReport>.Ok(report);
        }, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<DockLayoutResult> SavePresetAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return DockLayoutResult.Fail("INVALID_PRESET_NAME", "Preset name is required.");
        var exported = await ExportLayoutXmlAsync(cancellationToken).ConfigureAwait(false);
        if (!exported.Success || exported.Value is null) return DockLayoutResult.Fail(exported.Error!.Code, exported.Error.Message);
        await _persistence.SaveAsync(PresetKey(name), exported.Value, cancellationToken).ConfigureAwait(false);
        return DockLayoutResult.Ok();
    }

    /// <inheritdoc />
    public async ValueTask<DockLayoutResult> RestorePresetAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return DockLayoutResult.Fail("INVALID_PRESET_NAME", "Preset name is required.");
        var xml = await _persistence.LoadAsync(PresetKey(name), cancellationToken).ConfigureAwait(false);
        if (xml is null) return DockLayoutResult.Fail("PRESET_NOT_FOUND", $"Preset '{name}' was not found.");
        var result = await ImportLayoutXmlAsync(xml, cancellationToken).ConfigureAwait(false);
        return result.Success ? DockLayoutResult.Ok() : DockLayoutResult.Fail(result.Error!.Code, result.Error.Message);
    }

    /// <inheritdoc />
    public async ValueTask<DockLayoutResult> DeletePresetAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return DockLayoutResult.Fail("INVALID_PRESET_NAME", "Preset name is required.");
        await _persistence.DeleteAsync(PresetKey(name), cancellationToken).ConfigureAwait(false);
        return DockLayoutResult.Ok();
    }

    /// <inheritdoc />
    public ValueTask<DockLayoutSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(ct => _store.GetSnapshot(), cancellationToken);

    internal async ValueTask RestoreAutoSaveAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.AutoRestore) return;
        var xml = await _persistence.LoadAsync(_options.PersistenceKey, cancellationToken).ConfigureAwait(false);
        if (xml is not null) await ImportLayoutXmlAsync(xml, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _saveDebounceCancellation?.Cancel();
        _saveDebounceCancellation?.Dispose();
        _saveDebounceCancellation = null;
        _store.MarkDisposed();
        _store.Changed -= HandleStoreChanged;
        LayoutChanged = null;
        TabOpened = null;
        TabActivated = null;
        TabClosed = null;
        PanelCreated = null;
        PanelRemoved = null;
        LayoutRestored = null;
        await _queue.DisposeAsync().ConfigureAwait(false);
    }

    private void HandleStoreChanged(object? sender, DockLayoutChangedEventArgs args)
    {
        LayoutChanged?.Invoke(this, args);
        if (!_options.AutoSave || args.Reason == DockLayoutChangeReason.Dispose) return;
        _saveDebounceCancellation?.Cancel();
        _saveDebounceCancellation?.Dispose();
        _saveDebounceCancellation = new CancellationTokenSource();
        _ = SaveSnapshotAfterDebounceAsync(args.Snapshot, _saveDebounceCancellation.Token);
    }

    private async Task SaveSnapshotAfterDebounceAsync(DockLayoutSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            var xml = _serializer.Serialize(snapshot);
            if (xml.Success && xml.Value is not null)
            {
                await _persistence.SaveAsync(_options.PersistenceKey, xml.Value, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (InvalidOperationException)
        {
            // Persistence is non-fatal; the in-memory state remains valid.
        }
    }

    private string PresetKey(string name) => $"{_options.PersistenceKey}.preset.{name.Trim()}";

    private DockTabEventArgs TabArgs(EditorTabInfo tab, DockLayoutChangeReason reason) => new()
    {
        InstanceId = InstanceId,
        Reason = reason,
        Revision = _store.GetSnapshot().Revision,
        Tab = tab
    };
}
