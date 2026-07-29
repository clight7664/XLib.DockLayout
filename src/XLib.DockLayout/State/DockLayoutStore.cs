using XLib.DockLayout.Models;

namespace XLib.DockLayout.State;

internal sealed class DockLayoutStore
{
    private readonly string _version;
    private readonly List<PanelState> _panels = new();
    private readonly List<ViewState> _views = new();
    private readonly List<string> _recentTabs = new();
    private EditorNodeState _editorRoot = null!;
    private DockLayoutGeometry _geometry = new();
    private long _revision;
    private bool _disposed;

    public DockLayoutStore(string instanceId, DockLayoutDefinition definition)
    {
        InstanceId = instanceId;
        _version = definition.Version;
        BuildInitialState(definition);
        EnsureActivePanels();
        var validation = DockLayoutInvariantValidator.Validate(GetSnapshot());
        if (!validation.Success)
        {
            throw new ArgumentException(validation.Error?.Message, nameof(definition));
        }
    }

    public string InstanceId { get; }

    public event EventHandler<DockLayoutChangedEventArgs>? Changed;

    public DockLayoutSnapshot GetSnapshot()
    {
        var panelInfos = _panels
            .OrderBy(panel => panel.Region)
            .ThenBy(panel => panel.Order)
            .Select(panel => panel.ToInfo())
            .ToArray();
        var viewInfos = _views
            .OrderBy(view => view.Order)
            .Select(view => view.ToInfo())
            .ToArray();
        return new DockLayoutSnapshot
        {
            Version = _version,
            InstanceId = InstanceId,
            Revision = _revision,
            Panels = panelInfos,
            EditorViews = viewInfos,
            EditorRoot = _editorRoot.ToInfo(),
            Geometry = _geometry
        };
    }

    public DockLayoutResult<EditorTabInfo> OpenTab(OpenEditorTabRequest request, TabDedupMode dedupMode)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult<EditorTabInfo>.Fail(unusable.Code, unusable.Message);
        var view = _views.FirstOrDefault(item => item.Id == request.ViewId);
        if (view is null) return DockLayoutResult<EditorTabInfo>.Fail("VIEW_NOT_FOUND", $"EditorView '{request.ViewId}' was not found.");

        if (dedupMode == TabDedupMode.ActivateExisting)
        {
            var duplicate = view.Tabs.FirstOrDefault(tab =>
                tab.ContentId == request.ContentId && tab.DedupParameter == request.DedupParameter);
            if (duplicate is not null)
            {
                if (!string.Equals(view.ActiveTabId, duplicate.Id, StringComparison.Ordinal))
                {
                    ActivateInternal(duplicate);
                    Commit(DockLayoutChangeReason.Api, duplicate.Id);
                }
                return DockLayoutResult<EditorTabInfo>.Ok(duplicate.ToInfo(view.Id));
            }
        }

        var tabId = string.IsNullOrWhiteSpace(request.TabId) ? $"tab-{Guid.NewGuid():N}" : request.TabId.Trim();
        if (_views.SelectMany(item => item.Tabs).Any(tab => tab.Id == tabId))
        {
            return DockLayoutResult<EditorTabInfo>.Fail("TAB_ID_CONFLICT", $"EditorTab ID '{tabId}' already exists.");
        }

        var tabState = new TabState
        {
            Id = tabId,
            ContentId = request.ContentId,
            Title = request.Title,
            Icon = request.Icon,
            Closable = request.Closable,
            Pinned = request.Pinned,
            DedupParameter = request.DedupParameter,
            Parameters = new Dictionary<string, object?>(request.Parameters),
            LastAccessUtc = DateTimeOffset.UtcNow
        };
        view.Tabs.Add(tabState);
        SortPinned(view);
        ActivateInternal(tabState);
        Commit(DockLayoutChangeReason.Api, tabState.Id);
        return DockLayoutResult<EditorTabInfo>.Ok(tabState.ToInfo(view.Id));
    }

    public DockLayoutResult CloseTab(string tabId, bool recentAccess)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        var located = FindTab(tabId);
        if (located is null) return DockLayoutResult.Fail("TAB_NOT_FOUND", $"EditorTab '{tabId}' was not found.");
        var (view, tab, index) = located.Value;
        if (!tab.Closable) return DockLayoutResult.Fail("TAB_NOT_CLOSABLE", $"EditorTab '{tabId}' cannot be closed.");

        var wasActive = view.ActiveTabId == tabId;
        view.Tabs.RemoveAt(index);
        _recentTabs.RemoveAll(id => id == tabId);
        if (wasActive)
        {
            var next = recentAccess
                ? FindMostRecent(view)
                : (index < view.Tabs.Count ? view.Tabs[index] : view.Tabs.LastOrDefault());
            view.ActiveTabId = next?.Id;
        }

        PruneEmptyDynamicViewsAndCompressTree();
        Commit(DockLayoutChangeReason.Api, tabId);
        return DockLayoutResult.Ok();
    }

    public DockLayoutResult ActivateTab(string tabId)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        var located = FindTab(tabId);
        if (located is null) return DockLayoutResult.Fail("TAB_NOT_FOUND", $"EditorTab '{tabId}' was not found.");
        if (string.Equals(located.Value.View.ActiveTabId, tabId, StringComparison.Ordinal))
        {
            return DockLayoutResult.Ok();
        }
        ActivateInternal(located.Value.Tab);
        Commit(DockLayoutChangeReason.Api, tabId);
        return DockLayoutResult.Ok();
    }

    public IReadOnlyList<EditorTabInfo> FindTabs(TabQuery query)
    {
        return _views.SelectMany(view => view.Tabs.Select((tab, index) => tab.ToInfo(view.Id) with
        {
            Active = tab.Id == view.ActiveTabId,
            Order = index
        }))
            .Where(tab => query.TabId is null || tab.Id == query.TabId)
            .Where(tab => query.ViewId is null || tab.ViewId == query.ViewId)
            .Where(tab => query.ContentId is null || tab.ContentId == query.ContentId)
            .Where(tab => query.DedupParameter is null || tab.DedupParameter == query.DedupParameter)
            .OrderBy(tab => tab.ViewId, StringComparer.Ordinal)
            .ThenBy(tab => tab.Order)
            .ToArray();
    }

    public DockLayoutResult SetTabPinned(string tabId, bool pinned)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        var located = FindTab(tabId);
        if (located is null) return DockLayoutResult.Fail("TAB_NOT_FOUND", $"EditorTab '{tabId}' was not found.");
        located.Value.Tab.Pinned = pinned;
        SortPinned(located.Value.View);
        Commit(DockLayoutChangeReason.Api, tabId);
        return DockLayoutResult.Ok();
    }

    public DockLayoutResult MoveTab(string tabId, string targetViewId, DockDirection direction, int targetIndex)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        var located = FindTab(tabId);
        if (located is null) return DockLayoutResult.Fail("TAB_NOT_FOUND", $"EditorTab '{tabId}' was not found.");
        var target = _views.FirstOrDefault(view => view.Id == targetViewId);
        if (target is null) return DockLayoutResult.Fail("VIEW_NOT_FOUND", $"EditorView '{targetViewId}' was not found.");

        var source = located.Value.View;
        var tab = located.Value.Tab;
        source.Tabs.Remove(tab);

        ViewState destination = target;
        if (direction != DockDirection.Center)
        {
            var orientation = direction is DockDirection.Left or DockDirection.Right
                ? SplitOrientation.Vertical
                : SplitOrientation.Horizontal;
            destination = new ViewState
            {
                Id = $"view-{Guid.NewGuid():N}",
                Lifecycle = EditorViewLifecycle.Dynamic,
                Ratio = 0.5d,
                Orientation = orientation,
                Order = target.Order + (direction is DockDirection.Right or DockDirection.Bottom ? 1 : 0)
            };
            _views.Add(destination);
            var targetLeaf = FindLeaf(_editorRoot, target.Id)
                ?? throw new InvalidOperationException($"EditorView '{target.Id}' has no split-tree leaf.");
            var createdLeaf = new EditorLeafState($"leaf-{Guid.NewGuid():N}", destination.Id);
            var targetReplacement = direction is DockDirection.Left or DockDirection.Top
                ? new EditorSplitState($"split-{Guid.NewGuid():N}", orientation, 0.5d, SplitCollapsedSide.None, createdLeaf, targetLeaf.Clone())
                : new EditorSplitState($"split-{Guid.NewGuid():N}", orientation, 0.5d, SplitCollapsedSide.None, targetLeaf.Clone(), createdLeaf);
            _editorRoot = ReplaceLeaf(_editorRoot, target.Id, targetReplacement);
            ReindexViewsFromTree();
        }

        targetIndex = targetIndex < 0 ? destination.Tabs.Count : Math.Min(targetIndex, destination.Tabs.Count);
        destination.Tabs.Insert(targetIndex, tab);
        SortPinned(destination);
        ActivateInternal(tab);
        if (!ReferenceEquals(source, destination))
        {
            source.ActiveTabId = source.Tabs.FirstOrDefault()?.Id;
        }
        PruneEmptyDynamicViewsAndCompressTree();
        Commit(DockLayoutChangeReason.User, tabId);
        return DockLayoutResult.Ok();
    }

    public DockLayoutResult<string> SplitView(string viewId, SplitOrientation orientation)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult<string>.Fail(unusable.Code, unusable.Message);
        var view = _views.FirstOrDefault(item => item.Id == viewId);
        if (view is null) return DockLayoutResult<string>.Fail("VIEW_NOT_FOUND", $"EditorView '{viewId}' was not found.");
        var created = new ViewState
        {
            Id = $"view-{Guid.NewGuid():N}",
            Lifecycle = EditorViewLifecycle.Dynamic,
            Ratio = 0.5d,
            Orientation = orientation,
            Order = view.Order + 1
        };
        _views.Add(created);
        var targetLeaf = FindLeaf(_editorRoot, view.Id)
            ?? throw new InvalidOperationException($"EditorView '{view.Id}' has no split-tree leaf.");
        _editorRoot = ReplaceLeaf(_editorRoot, view.Id,
            new EditorSplitState($"split-{Guid.NewGuid():N}", orientation, 0.5d, SplitCollapsedSide.None,
                targetLeaf.Clone(), new EditorLeafState($"leaf-{Guid.NewGuid():N}", created.Id)));
        ReindexViewsFromTree();
        Commit(DockLayoutChangeReason.Api, created.Id);
        return DockLayoutResult<string>.Ok(created.Id);
    }

    public DockLayoutResult ResizeEditorSplit(string splitId, double firstRatio)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        var split = FindSplit(_editorRoot, splitId);
        if (split is null) return DockLayoutResult.Fail("SPLIT_NOT_FOUND", $"Editor split '{splitId}' was not found.");
        split.FirstRatio = Math.Clamp(firstRatio, 0.05d, 0.95d);
        split.CollapsedSide = SplitCollapsedSide.None;
        Commit(DockLayoutChangeReason.User, splitId);
        return DockLayoutResult.Ok();
    }

    public DockLayoutResult ToggleEditorSplitCollapse(string splitId)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        var split = FindSplit(_editorRoot, splitId);
        if (split is null) return DockLayoutResult.Fail("SPLIT_NOT_FOUND", $"Editor split '{splitId}' was not found.");
        split.CollapsedSide = split.CollapsedSide == SplitCollapsedSide.Second
            ? SplitCollapsedSide.None
            : SplitCollapsedSide.Second;
        Commit(DockLayoutChangeReason.User, splitId);
        return DockLayoutResult.Ok();
    }

    public DockLayoutResult ResizeLayoutSplitter(string splitterId, double ratio)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        if (string.IsNullOrWhiteSpace(splitterId)) return DockLayoutResult.Fail("INVALID_SPLITTER_ID", "Splitter ID is required.");
        if (double.IsNaN(ratio) || double.IsInfinity(ratio)) return DockLayoutResult.Fail("INVALID_RATIO", "Splitter ratio must be finite.");
        _geometry = splitterId switch
        {
            "layout-left-editor" => _geometry with { LeftDockRatio = Math.Clamp(ratio, 0.05d, 0.60d), LeftDockCollapsed = false },
            "layout-editor-right" => _geometry with { RightDockRatio = Math.Clamp(ratio, 0.05d, 0.60d), RightDockCollapsed = false },
            "layout-work-bottom" => _geometry with { BottomDockRatio = Math.Clamp(ratio, 0.05d, 0.70d), BottomDockCollapsed = false },
            "layout-left-regions" => _geometry with { LeftUpperRatio = Math.Clamp(ratio, 0.05d, 0.95d), LeftLowerCollapsed = false },
            "layout-right-regions" => _geometry with { RightUpperRatio = Math.Clamp(ratio, 0.05d, 0.95d), RightLowerCollapsed = false },
            "layout-bottom-regions" => _geometry with { BottomLeftRatio = Math.Clamp(ratio, 0.05d, 0.95d), BottomRightCollapsed = false },
            _ => _geometry
        };
        if (splitterId is not ("layout-left-editor" or "layout-editor-right" or "layout-work-bottom" or
            "layout-left-regions" or "layout-right-regions" or "layout-bottom-regions"))
        {
            return DockLayoutResult.Fail("SPLITTER_NOT_FOUND", $"Layout splitter '{splitterId}' was not found.");
        }
        Commit(DockLayoutChangeReason.User, splitterId);
        return DockLayoutResult.Ok();
    }

    public DockLayoutResult ToggleLayoutSplitterCollapse(string splitterId)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        _geometry = splitterId switch
        {
            "layout-left-editor" => _geometry with { LeftDockCollapsed = !_geometry.LeftDockCollapsed },
            "layout-editor-right" => _geometry with { RightDockCollapsed = !_geometry.RightDockCollapsed },
            "layout-work-bottom" => _geometry with { BottomDockCollapsed = !_geometry.BottomDockCollapsed },
            "layout-left-regions" => _geometry with { LeftLowerCollapsed = !_geometry.LeftLowerCollapsed },
            "layout-right-regions" => _geometry with { RightLowerCollapsed = !_geometry.RightLowerCollapsed },
            "layout-bottom-regions" => _geometry with { BottomRightCollapsed = !_geometry.BottomRightCollapsed },
            _ => _geometry
        };
        if (splitterId is not ("layout-left-editor" or "layout-editor-right" or "layout-work-bottom" or
            "layout-left-regions" or "layout-right-regions" or "layout-bottom-regions"))
        {
            return DockLayoutResult.Fail("SPLITTER_NOT_FOUND", $"Layout splitter '{splitterId}' was not found.");
        }
        Commit(DockLayoutChangeReason.User, splitterId);
        return DockLayoutResult.Ok();
    }

    public DockLayoutResult CollapsePanel(string panelId)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        var panel = _panels.FirstOrDefault(item => item.Id == panelId);
        if (panel is null) return DockLayoutResult.Fail("PANEL_NOT_FOUND", $"DockPanel '{panelId}' was not found.");
        panel.Collapsed = true;
        panel.Active = false;
        EnsureActivePanel(panel.Region);
        Commit(DockLayoutChangeReason.Api, panelId);
        return DockLayoutResult.Ok();
    }

    public DockLayoutResult ExpandPanel(string panelId)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        var panel = _panels.FirstOrDefault(item => item.Id == panelId);
        if (panel is null) return DockLayoutResult.Fail("PANEL_NOT_FOUND", $"DockPanel '{panelId}' was not found.");
        foreach (var sibling in _panels.Where(item => item.Region == panel.Region)) sibling.Active = false;
        panel.Visible = true;
        panel.Collapsed = false;
        panel.Active = true;
        Commit(DockLayoutChangeReason.Api, panelId);
        return DockLayoutResult.Ok();
    }

    public DockLayoutResult SetPanelVisibility(string panelId, bool visible)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        var panel = _panels.FirstOrDefault(item => item.Id == panelId);
        if (panel is null) return DockLayoutResult.Fail("PANEL_NOT_FOUND", $"DockPanel '{panelId}' was not found.");
        panel.Visible = visible;
        panel.Active = visible && !panel.Collapsed;
        if (panel.Active)
        {
            foreach (var sibling in _panels.Where(item => item.Region == panel.Region && item.Id != panel.Id)) sibling.Active = false;
        }
        else
        {
            EnsureActivePanel(panel.Region);
        }
        Commit(DockLayoutChangeReason.Api, panelId);
        return DockLayoutResult.Ok();
    }

    public DockLayoutResult SetToolBarEntryVisibility(string panelId, bool visible)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        var panel = _panels.FirstOrDefault(item => item.Id == panelId);
        if (panel is null) return DockLayoutResult.Fail("PANEL_NOT_FOUND", $"DockPanel '{panelId}' was not found.");
        panel.ToolBarVisible = visible;
        Commit(DockLayoutChangeReason.Api, panelId);
        return DockLayoutResult.Ok();
    }

    public DockLayoutResult SetPanelAutoHide(string panelId, bool autoHide)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        var panel = _panels.FirstOrDefault(item => item.Id == panelId);
        if (panel is null) return DockLayoutResult.Fail("PANEL_NOT_FOUND", $"DockPanel '{panelId}' was not found.");
        panel.AutoHide = autoHide;
        panel.Collapsed = autoHide;
        panel.Active = !autoHide && panel.Visible;
        if (panel.Active)
        {
            foreach (var sibling in _panels.Where(item => item.Region == panel.Region && item.Id != panel.Id)) sibling.Active = false;
        }
        else
        {
            EnsureActivePanel(panel.Region);
        }
        Commit(DockLayoutChangeReason.Api, panelId);
        return DockLayoutResult.Ok();
    }

    public DockLayoutResult MovePanel(string panelId, DockRegionName target, int targetIndex)
    {
        if (!TryUsable(out var unusable)) return DockLayoutResult.Fail(unusable.Code, unusable.Message);
        var panel = _panels.FirstOrDefault(item => item.Id == panelId);
        if (panel is null) return DockLayoutResult.Fail("PANEL_NOT_FOUND", $"DockPanel '{panelId}' was not found.");
        var sourceRegion = panel.Region;
        panel.Region = target;
        panel.Visible = true;
        panel.Collapsed = false;
        panel.AutoHide = false;
        foreach (var targetPanel in _panels.Where(item => item.Region == target && item.Id != panelId)) targetPanel.Active = false;
        panel.Active = true;
        ReorderPanels(target, panel, targetIndex);
        EnsureActivePanel(sourceRegion);
        Commit(DockLayoutChangeReason.User, panelId);
        return DockLayoutResult.Ok();
    }

    public DockLayoutResult Replace(DockLayoutSnapshot snapshot, DockLayoutChangeReason reason)
    {
        var candidate = snapshot with { InstanceId = InstanceId, Revision = _revision + 1 };
        var validation = DockLayoutInvariantValidator.Validate(candidate);
        if (!validation.Success) return validation;

        _panels.Clear();
        _views.Clear();
        foreach (var panel in candidate.Panels) _panels.Add(PanelState.FromInfo(panel));
        foreach (var view in candidate.EditorViews) _views.Add(ViewState.FromInfo(view));
        _editorRoot = EditorNodeState.FromInfo(candidate.EditorRoot);
        _geometry = candidate.Geometry;
        ReindexViewsFromTree();
        _revision = candidate.Revision;
        Changed?.Invoke(this, new DockLayoutChangedEventArgs
        {
            InstanceId = InstanceId,
            Reason = reason,
            Revision = _revision,
            Snapshot = GetSnapshot()
        });
        return DockLayoutResult.Ok();
    }

    public void MarkDisposed()
    {
        if (_disposed) return;
        _disposed = true;
        _revision++;
        Changed?.Invoke(this, new DockLayoutChangedEventArgs
        {
            InstanceId = InstanceId,
            Reason = DockLayoutChangeReason.Dispose,
            Revision = _revision,
            Snapshot = GetSnapshot()
        });
    }

    private void BuildInitialState(DockLayoutDefinition definition)
    {
        _geometry = definition.Geometry;
        var panelIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in definition.Panels.GroupBy(panel => panel.Region))
        {
            var order = 0;
            foreach (var definitionPanel in group)
            {
                if (!panelIds.Add(definitionPanel.Id)) throw new ArgumentException($"Duplicate DockPanel ID '{definitionPanel.Id}'.", nameof(definition));
                _panels.Add(new PanelState
                {
                    Id = definitionPanel.Id,
                    Region = definitionPanel.Region,
                    ContentId = definitionPanel.ContentId,
                    Title = definitionPanel.Title ?? definitionPanel.Id,
                    Icon = definitionPanel.Icon,
                    Lifecycle = definitionPanel.Lifecycle,
                    Ratio = NormalizeRatio(definitionPanel.InitialRatio),
                    MinSize = Math.Max(0, definitionPanel.MinSize),
                    MaxSize = Math.Max(definitionPanel.MinSize, definitionPanel.MaxSize),
                    Visible = definitionPanel.Visible,
                    ToolBarVisible = definitionPanel.ToolBarVisible,
                    Collapsed = definitionPanel.Collapsed,
                    Active = definitionPanel.Visible && !definitionPanel.Collapsed && order == 0,
                    AutoHide = definitionPanel.AutoHide,
                    Order = order++,
                    Parameters = new Dictionary<string, object?>(definitionPanel.Parameters)
                });
            }
        }

        var viewIds = new HashSet<string>(StringComparer.Ordinal);
        var tabIds = new HashSet<string>(StringComparer.Ordinal);
        var viewOrder = 0;
        foreach (var definitionView in definition.EditorViews)
        {
            if (!viewIds.Add(definitionView.Id)) throw new ArgumentException($"Duplicate EditorView ID '{definitionView.Id}'.", nameof(definition));
            var view = new ViewState
            {
                Id = definitionView.Id,
                Lifecycle = definitionView.Lifecycle,
                Ratio = NormalizeRatio(definitionView.Ratio),
                Orientation = definitionView.Orientation,
                Order = viewOrder++
            };
            foreach (var definitionTab in definitionView.Tabs)
            {
                var tabId = string.IsNullOrWhiteSpace(definitionTab.Id) ? $"tab-{Guid.NewGuid():N}" : definitionTab.Id;
                if (!tabIds.Add(tabId)) throw new ArgumentException($"Duplicate EditorTab ID '{tabId}'.", nameof(definition));
                view.Tabs.Add(new TabState
                {
                    Id = tabId,
                    ContentId = definitionTab.ContentId,
                    Title = definitionTab.Title,
                    Icon = definitionTab.Icon,
                    Closable = definitionTab.Closable,
                    Pinned = definitionTab.Pinned,
                    DedupParameter = definitionTab.DedupParameter,
                    Parameters = new Dictionary<string, object?>(definitionTab.Parameters),
                    LastAccessUtc = DateTimeOffset.UtcNow
                });
            }
            SortPinned(view);
            view.ActiveTabId = definitionView.ActiveTabId is not null && view.Tabs.Any(tab => tab.Id == definitionView.ActiveTabId)
                ? definitionView.ActiveTabId
                : view.Tabs.FirstOrDefault()?.Id;
            _views.Add(view);
        }

        if (_views.Count == 0)
        {
            _views.Add(new ViewState { Id = "view-main", Lifecycle = EditorViewLifecycle.Template, Ratio = 1d, Order = 0 });
        }

        _editorRoot = definition.EditorRoot is null
            ? BuildDefaultTree(_views)
            : EditorNodeState.FromDefinition(definition.EditorRoot);
        ReindexViewsFromTree();
    }

    private (ViewState View, TabState Tab, int Index)? FindTab(string tabId)
    {
        foreach (var view in _views)
        {
            var index = view.Tabs.FindIndex(tab => tab.Id == tabId);
            if (index >= 0) return (view, view.Tabs[index], index);
        }
        return null;
    }

    private void ActivateInternal(TabState tab)
    {
        foreach (var view in _views)
        {
            if (view.Tabs.Contains(tab)) view.ActiveTabId = tab.Id;
        }
        tab.LastAccessUtc = DateTimeOffset.UtcNow;
        _recentTabs.RemoveAll(id => id == tab.Id);
        _recentTabs.Insert(0, tab.Id);
        if (_recentTabs.Count > 100) _recentTabs.RemoveRange(100, _recentTabs.Count - 100);
    }

    private TabState? FindMostRecent(ViewState view)
    {
        foreach (var id in _recentTabs)
        {
            var tab = view.Tabs.FirstOrDefault(item => item.Id == id);
            if (tab is not null) return tab;
        }
        return view.Tabs.FirstOrDefault();
    }

    private void PruneEmptyDynamicViewsAndCompressTree()
    {
        var removable = _views
            .Where(view => view.Tabs.Count == 0 && view.Lifecycle == EditorViewLifecycle.Dynamic)
            .ToArray();

        foreach (var view in removable)
        {
            if (_views.Count <= 1) break;
            _views.Remove(view);
            _editorRoot = RemoveLeaf(_editorRoot, view.Id)
                ?? new EditorLeafState($"leaf-{Guid.NewGuid():N}", _views[0].Id);
        }

        _editorRoot = CompressTree(_editorRoot);
        ReindexViewsFromTree();
    }

    private static EditorNodeState CompressTree(EditorNodeState node)
    {
        if (node is EditorLeafState) return node;
        var split = (EditorSplitState)node;
        split.First = CompressTree(split.First);
        split.Second = CompressTree(split.Second);
        return split;
    }

    private void SortPinned(ViewState view)
    {
        var ordered = view.Tabs.Select((tab, index) => (tab, index))
            .OrderByDescending(item => item.tab.Pinned)
            .ThenBy(item => item.index)
            .Select(item => item.tab)
            .ToArray();
        view.Tabs.Clear();
        view.Tabs.AddRange(ordered);
    }

    private void ReorderPanels(DockRegionName region, PanelState panel, int targetIndex)
    {
        var group = _panels.Where(item => item.Region == region && item.Id != panel.Id)
            .OrderBy(item => item.Order).ToList();
        targetIndex = targetIndex < 0 ? group.Count : Math.Min(targetIndex, group.Count);
        group.Insert(targetIndex, panel);
        for (var index = 0; index < group.Count; index++) group[index].Order = index;
    }

    private void ReindexViews()
    {
        for (var index = 0; index < _views.Count; index++) _views[index].Order = index;
    }

    private void ReindexViewsFromTree()
    {
        var viewIds = new List<string>();
        CollectViewIds(_editorRoot, viewIds);
        var ordered = viewIds.Select(id => _views.FirstOrDefault(view => view.Id == id))
            .Where(view => view is not null)
            .Cast<ViewState>()
            .ToList();
        foreach (var view in _views.Where(view => !viewIds.Any(id => string.Equals(id, view.Id, StringComparison.Ordinal)))) ordered.Add(view);
        _views.Clear();
        _views.AddRange(ordered);
        ReindexViews();
    }

    private static EditorNodeState BuildDefaultTree(IReadOnlyList<ViewState> views)
    {
        EditorNodeState root = new EditorLeafState($"leaf-{Guid.NewGuid():N}", views[0].Id);
        for (var index = 1; index < views.Count; index++)
        {
            root = new EditorSplitState(
                $"split-{Guid.NewGuid():N}",
                views[index - 1].Orientation,
                Math.Clamp(views[index - 1].Ratio, 0.05d, 0.95d),
                SplitCollapsedSide.None,
                root,
                new EditorLeafState($"leaf-{Guid.NewGuid():N}", views[index].Id));
        }
        return root;
    }

    private static void CollectViewIds(EditorNodeState node, ICollection<string> viewIds)
    {
        if (node is EditorLeafState leaf)
        {
            viewIds.Add(leaf.ViewId);
            return;
        }
        var split = (EditorSplitState)node;
        CollectViewIds(split.First, viewIds);
        CollectViewIds(split.Second, viewIds);
    }

    private static EditorLeafState? FindLeaf(EditorNodeState node, string viewId)
    {
        if (node is EditorLeafState leaf) return leaf.ViewId == viewId ? leaf : null;
        var split = (EditorSplitState)node;
        return FindLeaf(split.First, viewId) ?? FindLeaf(split.Second, viewId);
    }

    private static EditorSplitState? FindSplit(EditorNodeState node, string splitId)
    {
        if (node is EditorLeafState) return null;
        var split = (EditorSplitState)node;
        if (split.Id == splitId) return split;
        return FindSplit(split.First, splitId) ?? FindSplit(split.Second, splitId);
    }

    private static EditorNodeState ReplaceLeaf(EditorNodeState node, string viewId, EditorNodeState replacement)
    {
        if (node is EditorLeafState leaf) return leaf.ViewId == viewId ? replacement : leaf;
        var split = (EditorSplitState)node;
        split.First = ReplaceLeaf(split.First, viewId, replacement);
        split.Second = ReplaceLeaf(split.Second, viewId, replacement);
        return split;
    }

    private static EditorNodeState? RemoveLeaf(EditorNodeState node, string viewId)
    {
        if (node is EditorLeafState leaf) return leaf.ViewId == viewId ? null : leaf;
        var split = (EditorSplitState)node;
        var first = RemoveLeaf(split.First, viewId);
        var second = RemoveLeaf(split.Second, viewId);
        if (first is null) return second;
        if (second is null) return first;
        split.First = first;
        split.Second = second;
        return split;
    }

    private void EnsureActivePanels()
    {
        foreach (var region in Enum.GetValues<DockRegionName>()) EnsureActivePanel(region);
    }

    private void EnsureActivePanel(DockRegionName region)
    {
        var panels = _panels.Where(panel => panel.Region == region).OrderBy(panel => panel.Order).ToArray();
        if (panels.Any(panel => panel.Active && panel.Visible && !panel.Collapsed)) return;
        foreach (var panel in panels) panel.Active = false;
        var candidate = panels.FirstOrDefault(panel => panel.Visible && !panel.Collapsed);
        if (candidate is not null) candidate.Active = true;
    }

    private void Commit(DockLayoutChangeReason reason, string? objectId)
    {
        _revision++;
        var snapshot = GetSnapshot();
        var validation = DockLayoutInvariantValidator.Validate(snapshot);
        if (!validation.Success) throw new InvalidOperationException(validation.Error?.Message);
        Changed?.Invoke(this, new DockLayoutChangedEventArgs
        {
            InstanceId = InstanceId,
            Reason = reason,
            Revision = _revision,
            ObjectId = objectId,
            Snapshot = snapshot
        });
    }

    private bool TryUsable(out DockLayoutError error)
    {
        if (_disposed)
        {
            error = new DockLayoutError("LAYOUT_DISPOSED", "The DockLayout instance has been disposed.");
            return false;
        }
        error = new DockLayoutError(string.Empty, string.Empty);
        return true;
    }

    private static double NormalizeRatio(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) || value < 0d ? 0d : value;

    private abstract class EditorNodeState
    {
        protected EditorNodeState(string id) => Id = id;
        public string Id { get; }
        public abstract EditorNodeInfo ToInfo();
        public abstract EditorNodeState Clone();

        public static EditorNodeState FromInfo(EditorNodeInfo node) => node switch
        {
            EditorLeafInfo leaf => new EditorLeafState(leaf.Id, leaf.ViewId),
            EditorSplitInfo split => new EditorSplitState(split.Id, split.Orientation, split.FirstRatio, split.CollapsedSide, FromInfo(split.First), FromInfo(split.Second)),
            _ => throw new ArgumentOutOfRangeException(nameof(node))
        };

        public static EditorNodeState FromDefinition(EditorNodeDefinition node) => node switch
        {
            EditorLeafDefinition leaf => new EditorLeafState(leaf.Id, leaf.ViewId),
            EditorSplitDefinition split => new EditorSplitState(split.Id, split.Orientation, split.FirstRatio, split.CollapsedSide, FromDefinition(split.First), FromDefinition(split.Second)),
            _ => throw new ArgumentOutOfRangeException(nameof(node))
        };
    }

    private sealed class EditorLeafState(string id, string viewId) : EditorNodeState(id)
    {
        public string ViewId { get; } = viewId;
        public override EditorNodeInfo ToInfo() => new EditorLeafInfo { Id = Id, ViewId = ViewId };
        public override EditorNodeState Clone() => new EditorLeafState(Id, ViewId);
    }

    private sealed class EditorSplitState(
        string id,
        SplitOrientation orientation,
        double firstRatio,
        SplitCollapsedSide collapsedSide,
        EditorNodeState first,
        EditorNodeState second) : EditorNodeState(id)
    {
        public SplitOrientation Orientation { get; } = orientation;
        public double FirstRatio { get; set; } = Math.Clamp(firstRatio, 0.05d, 0.95d);
        public SplitCollapsedSide CollapsedSide { get; set; } = collapsedSide;
        public EditorNodeState First { get; set; } = first;
        public EditorNodeState Second { get; set; } = second;
        public override EditorNodeInfo ToInfo() => new EditorSplitInfo
        {
            Id = Id,
            Orientation = Orientation,
            FirstRatio = FirstRatio,
            CollapsedSide = CollapsedSide,
            First = First.ToInfo(),
            Second = Second.ToInfo()
        };
        public override EditorNodeState Clone() => new EditorSplitState(Id, Orientation, FirstRatio, CollapsedSide, First.Clone(), Second.Clone());
    }

    private sealed class PanelState
    {
        public required string Id { get; init; }
        public DockRegionName Region { get; set; }
        public string? ContentId { get; init; }
        public required string Title { get; init; }
        public string? Icon { get; init; }
        public DockPanelLifecycle Lifecycle { get; init; }
        public double Ratio { get; set; }
        public double MinSize { get; init; }
        public double MaxSize { get; init; }
        public bool Visible { get; set; }
        public bool ToolBarVisible { get; set; } = true;
        public bool Collapsed { get; set; }
        public bool Active { get; set; }
        public bool AutoHide { get; set; }
        public int Order { get; set; }
        public Dictionary<string, object?> Parameters { get; init; } = new();

        public DockPanelInfo ToInfo() => new()
        {
            Id = Id,
            Region = Region,
            ContentId = ContentId,
            Title = Title,
            Icon = Icon,
            Lifecycle = Lifecycle,
            Ratio = Ratio,
            MinSize = MinSize,
            MaxSize = MaxSize,
            Visible = Visible,
            ToolBarVisible = ToolBarVisible,
            Collapsed = Collapsed,
            Active = Active,
            AutoHide = AutoHide,
            Order = Order,
            Parameters = new Dictionary<string, object?>(Parameters)
        };

        public static PanelState FromInfo(DockPanelInfo panel) => new()
        {
            Id = panel.Id,
            Region = panel.Region,
            ContentId = panel.ContentId,
            Title = panel.Title,
            Icon = panel.Icon,
            Lifecycle = panel.Lifecycle,
            Ratio = panel.Ratio,
            MinSize = panel.MinSize,
            MaxSize = panel.MaxSize,
            Visible = panel.Visible,
            ToolBarVisible = panel.ToolBarVisible,
            Collapsed = panel.Collapsed,
            Active = panel.Active,
            AutoHide = panel.AutoHide,
            Order = panel.Order,
            Parameters = new Dictionary<string, object?>(panel.Parameters)
        };
    }

    private sealed class ViewState
    {
        public required string Id { get; init; }
        public EditorViewLifecycle Lifecycle { get; init; }
        public string? ActiveTabId { get; set; }
        public double Ratio { get; set; }
        public SplitOrientation Orientation { get; set; }
        public int Order { get; set; }
        public List<TabState> Tabs { get; } = new();

        public EditorViewInfo ToInfo() => new()
        {
            Id = Id,
            Lifecycle = Lifecycle,
            ActiveTabId = ActiveTabId,
            Ratio = Ratio,
            Orientation = Orientation,
            Order = Order,
            Tabs = Tabs.Select((tab, index) => tab.ToInfo(Id) with { Active = tab.Id == ActiveTabId, Order = index }).ToArray()
        };

        public static ViewState FromInfo(EditorViewInfo view)
        {
            var state = new ViewState
            {
                Id = view.Id,
                Lifecycle = view.Lifecycle,
                ActiveTabId = view.ActiveTabId,
                Ratio = view.Ratio,
                Orientation = view.Orientation,
                Order = view.Order
            };
            state.Tabs.AddRange(view.Tabs.OrderBy(tab => tab.Order).Select(TabState.FromInfo));
            return state;
        }
    }

    private sealed class TabState
    {
        public required string Id { get; init; }
        public required string ContentId { get; init; }
        public required string Title { get; init; }
        public string? Icon { get; init; }
        public bool Closable { get; init; }
        public bool Pinned { get; set; }
        public string? DedupParameter { get; init; }
        public Dictionary<string, object?> Parameters { get; init; } = new();
        public DateTimeOffset LastAccessUtc { get; set; }

        public EditorTabInfo ToInfo(string viewId) => new()
        {
            Id = Id,
            ViewId = viewId,
            ContentId = ContentId,
            Title = Title,
            Icon = Icon,
            Closable = Closable,
            Pinned = Pinned,
            DedupParameter = DedupParameter,
            Parameters = new Dictionary<string, object?>(Parameters)
        };

        public static TabState FromInfo(EditorTabInfo tab) => new()
        {
            Id = tab.Id,
            ContentId = tab.ContentId,
            Title = tab.Title,
            Icon = tab.Icon,
            Closable = tab.Closable,
            Pinned = tab.Pinned,
            DedupParameter = tab.DedupParameter,
            Parameters = new Dictionary<string, object?>(tab.Parameters),
            LastAccessUtc = DateTimeOffset.UtcNow
        };
    }
}
