namespace XLib.DockLayout.Models;

/// <summary>Defines one DockPanel in a movable Dock Region.</summary>
public sealed record DockPanelDefinition
{
    /// <summary>Gets the globally unique panel identifier.</summary>
    public required string Id { get; init; }
    /// <summary>Gets the target Dock Region.</summary>
    public required DockRegionName Region { get; init; }
    /// <summary>Gets the content registry identifier.</summary>
    public string? ContentId { get; init; }
    /// <summary>Gets the display title.</summary>
    public string? Title { get; init; }
    /// <summary>Gets an icon text or trusted asset identifier.</summary>
    public string? Icon { get; init; }
    /// <summary>Gets the panel lifecycle.</summary>
    public DockPanelLifecycle Lifecycle { get; init; } = DockPanelLifecycle.Template;
    /// <summary>Gets the initial normalized size ratio.</summary>
    public double InitialRatio { get; init; } = 1d;
    /// <summary>Gets the minimum pixel size.</summary>
    public double MinSize { get; init; } = 120d;
    /// <summary>Gets the maximum pixel size.</summary>
    public double MaxSize { get; init; } = 1200d;
    /// <summary>Gets whether the panel is initially visible.</summary>
    public bool Visible { get; init; } = true;
    /// <summary>Gets whether the panel has an LT/RT toolbar entry initially.</summary>
    public bool ToolBarVisible { get; init; } = true;
    /// <summary>Gets whether the panel is initially collapsed.</summary>
    public bool Collapsed { get; init; }
    /// <summary>Gets whether the panel uses auto-hide.</summary>
    public bool AutoHide { get; init; }
    /// <summary>Gets parameters supplied to the panel content component.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
}

/// <summary>Defines one EditorTab.</summary>
public sealed record EditorTabDefinition
{
    /// <summary>Gets the optional tab identifier. A new ID is generated when omitted.</summary>
    public string? Id { get; init; }
    /// <summary>Gets the registered content identifier.</summary>
    public required string ContentId { get; init; }
    /// <summary>Gets the visible title.</summary>
    public required string Title { get; init; }
    /// <summary>Gets an icon text or trusted asset identifier.</summary>
    public string? Icon { get; init; }
    /// <summary>Gets whether the tab can be closed.</summary>
    public bool Closable { get; init; } = true;
    /// <summary>Gets whether the tab is pinned.</summary>
    public bool Pinned { get; init; }
    /// <summary>Gets the optional deduplication parameter.</summary>
    public string? DedupParameter { get; init; }
    /// <summary>Gets parameters supplied to the content component.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
}

/// <summary>Defines one EditorView and its initial tabs.</summary>
public sealed record EditorViewDefinition
{
    /// <summary>Gets the globally unique view identifier.</summary>
    public required string Id { get; init; }
    /// <summary>Gets the view lifecycle.</summary>
    public EditorViewLifecycle Lifecycle { get; init; } = EditorViewLifecycle.Template;
    /// <summary>Gets the initial tabs.</summary>
    public IReadOnlyList<EditorTabDefinition> Tabs { get; init; } = Array.Empty<EditorTabDefinition>();
    /// <summary>Gets the initial active tab ID.</summary>
    public string? ActiveTabId { get; init; }
    /// <summary>Gets the normalized split ratio.</summary>
    public double Ratio { get; init; } = 1d;
    /// <summary>Gets the split orientation used when multiple root views exist.</summary>
    public SplitOrientation Orientation { get; init; } = SplitOrientation.Vertical;
}

/// <summary>Defines the declarative initial DockLayout structure.</summary>
public sealed record DockLayoutDefinition
{
    /// <summary>Gets the layout definition version.</summary>
    public string Version { get; init; } = "1.0.0";
    /// <summary>Gets the initial DockPanels.</summary>
    public IReadOnlyList<DockPanelDefinition> Panels { get; init; } = Array.Empty<DockPanelDefinition>();
    /// <summary>Gets the initial EditorViews.</summary>
    public IReadOnlyList<EditorViewDefinition> EditorViews { get; init; } = Array.Empty<EditorViewDefinition>();
    /// <summary>Gets an optional explicit EditorArea binary split tree.</summary>
    public EditorNodeDefinition? EditorRoot { get; init; }
    /// <summary>Gets initial top-level layout geometry.</summary>
    public DockLayoutGeometry Geometry { get; init; } = new();
}

/// <summary>Configures DockLayout behavior.</summary>
public sealed record DockLayoutOptions
{
    /// <summary>Gets the tab overflow mode.</summary>
    public TabOverflowMode TabOverflowMode { get; init; } = TabOverflowMode.Scroll;
    /// <summary>Gets the tab deduplication mode.</summary>
    public TabDedupMode TabDedupMode { get; init; } = TabDedupMode.ActivateExisting;
    /// <summary>Gets whether recent-access activation is enabled.</summary>
    public bool UseRecentAccessActivation { get; init; }
    /// <summary>Gets whether automatic persistence is enabled.</summary>
    public bool AutoSave { get; init; } = true;
    /// <summary>Gets whether the initial layout is restored automatically.</summary>
    public bool AutoRestore { get; init; }
    /// <summary>Gets the persistence key prefix.</summary>
    public string PersistenceKey { get; init; } = "xlibDockLayout.layout";
    /// <summary>Gets the pointer distance that starts a drag operation.</summary>
    public double DragThreshold { get; init; } = 4d;
    /// <summary>Gets the splitter keyboard step in pixels.</summary>
    public double SplitterKeyboardStep { get; init; } = 8d;
    /// <summary>Gets whether animation is enabled when user preferences allow it.</summary>
    public bool EnableAnimations { get; init; } = true;
    /// <summary>Gets whether explicit Dark/Light theme support is enabled.</summary>
    public bool EnableThemeSupport { get; init; }
    /// <summary>Gets the explicit theme used when theme support is enabled.</summary>
    public DockThemeMode Theme { get; init; } = DockThemeMode.Dark;
    /// <summary>Gets whether splitter collapse/expand behavior is enabled.</summary>
    public bool EnableSplitterCollapse { get; init; } = true;
    /// <summary>Gets whether splitter collapse/expand buttons are rendered.</summary>
    public bool ShowSplitterCollapseButtons { get; init; } = true;
}

/// <summary>Describes a request to open an EditorTab.</summary>
public sealed record OpenEditorTabRequest
{
    /// <summary>Gets the target EditorView ID.</summary>
    public required string ViewId { get; init; }
    /// <summary>Gets the optional requested tab ID.</summary>
    public string? TabId { get; init; }
    /// <summary>Gets the registered content ID.</summary>
    public required string ContentId { get; init; }
    /// <summary>Gets the visible title.</summary>
    public required string Title { get; init; }
    /// <summary>Gets an optional icon.</summary>
    public string? Icon { get; init; }
    /// <summary>Gets whether the tab can be closed.</summary>
    public bool Closable { get; init; } = true;
    /// <summary>Gets whether the tab is pinned.</summary>
    public bool Pinned { get; init; }
    /// <summary>Gets the optional deduplication parameter.</summary>
    public string? DedupParameter { get; init; }
    /// <summary>Gets content component parameters.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
}

/// <summary>Defines a query for EditorTabs.</summary>
public sealed record TabQuery
{
    /// <summary>Gets an optional tab ID.</summary>
    public string? TabId { get; init; }
    /// <summary>Gets an optional view ID.</summary>
    public string? ViewId { get; init; }
    /// <summary>Gets an optional content ID.</summary>
    public string? ContentId { get; init; }
    /// <summary>Gets an optional deduplication parameter.</summary>
    public string? DedupParameter { get; init; }
}
