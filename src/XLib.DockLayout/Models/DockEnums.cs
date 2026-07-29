namespace XLib.DockLayout.Models;

/// <summary>Identifies one of the six movable Dock Regions.</summary>
public enum DockRegionName
{
    /// <summary>Upper region in the left dock.</summary>
    LeftDockUpper,
    /// <summary>Lower region in the left dock.</summary>
    LeftDockLower,
    /// <summary>Upper region in the right dock.</summary>
    RightDockUpper,
    /// <summary>Lower region in the right dock.</summary>
    RightDockLower,
    /// <summary>Left region in the bottom dock.</summary>
    BottomLeftDock,
    /// <summary>Right region in the bottom dock.</summary>
    BottomRightDock
}

/// <summary>Defines whether an empty DockPanel is retained.</summary>
public enum DockPanelLifecycle
{
    /// <summary>Retains the panel when its content is empty.</summary>
    Template,
    /// <summary>Removes the panel when its content is empty.</summary>
    Dynamic
}

/// <summary>Defines whether an empty EditorView is retained.</summary>
public enum EditorViewLifecycle
{
    /// <summary>Retains the view when its final tab is removed.</summary>
    Template,
    /// <summary>Removes the view when its final tab is removed.</summary>
    Dynamic
}

/// <summary>Defines EditorTab overflow behavior.</summary>
public enum TabOverflowMode
{
    /// <summary>Uses horizontal scrolling.</summary>
    Scroll,
    /// <summary>Uses an overflow menu.</summary>
    Menu,
    /// <summary>Wraps headers onto multiple rows.</summary>
    Wrap
}

/// <summary>Defines duplicate EditorTab handling.</summary>
public enum TabDedupMode
{
    /// <summary>Activates an existing tab with the same deduplication key.</summary>
    ActivateExisting,
    /// <summary>Always creates a new tab.</summary>
    AllowDuplicates
}

/// <summary>Defines split orientation.</summary>
public enum SplitOrientation
{
    /// <summary>Places nodes above and below each other.</summary>
    Horizontal,
    /// <summary>Places nodes to the left and right of each other.</summary>
    Vertical
}

/// <summary>Defines a tab docking target direction.</summary>
public enum DockDirection
{
    /// <summary>Adds the tab to the target view.</summary>
    Center,
    /// <summary>Creates a view to the left.</summary>
    Left,
    /// <summary>Creates a view to the right.</summary>
    Right,
    /// <summary>Creates a view above.</summary>
    Top,
    /// <summary>Creates a view below.</summary>
    Bottom
}


/// <summary>Defines the explicitly selected DockLayout color theme.</summary>
public enum DockThemeMode
{
    /// <summary>Uses the dark color palette.</summary>
    Dark,
    /// <summary>Uses the light color palette.</summary>
    Light
}

/// <summary>Identifies a collapsed child of a binary editor split.</summary>
public enum SplitCollapsedSide
{
    /// <summary>Neither child is collapsed.</summary>
    None,
    /// <summary>The first child is collapsed.</summary>
    First,
    /// <summary>The second child is collapsed.</summary>
    Second
}

/// <summary>Defines an action selected from an EditorTab context menu.</summary>
public enum EditorTabContextAction
{
    /// <summary>Closes the selected tab.</summary>
    Close,
    /// <summary>Closes all other closable tabs in the same view.</summary>
    CloseOthers,
    /// <summary>Closes all closable tabs in the same view.</summary>
    CloseAll,
    /// <summary>Closes closable tabs to the left.</summary>
    CloseToLeft,
    /// <summary>Closes closable tabs to the right.</summary>
    CloseToRight,
    /// <summary>Creates a right split and duplicates the selected tab into it.</summary>
    SplitRight,
    /// <summary>Creates a right split and moves the selected tab into it.</summary>
    SplitAndMoveRight,
    /// <summary>Creates a lower split and duplicates the selected tab into it.</summary>
    SplitDown,
    /// <summary>Creates a lower split and moves the selected tab into it.</summary>
    SplitAndMoveDown
}

/// <summary>Defines why a layout change occurred.</summary>
public enum DockLayoutChangeReason
{
    /// <summary>The initial definition was materialized.</summary>
    Initialization,
    /// <summary>An end-user interaction caused the change.</summary>
    User,
    /// <summary>A programmatic API call caused the change.</summary>
    Api,
    /// <summary>An XML import caused the change.</summary>
    Import,
    /// <summary>A persisted layout or preset was restored.</summary>
    Restore,
    /// <summary>The initial layout was restored.</summary>
    Reset,
    /// <summary>The instance was disposed.</summary>
    Dispose
}
