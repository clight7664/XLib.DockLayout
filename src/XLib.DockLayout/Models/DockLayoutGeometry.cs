namespace XLib.DockLayout.Models;

/// <summary>Defines normalized top-level DockLayout geometry ratios.</summary>
public sealed record DockLayoutGeometry
{
    /// <summary>Gets the Work Area width assigned to the left dock.</summary>
    public double LeftDockRatio { get; init; } = 0.20d;

    /// <summary>Gets the Work Area width assigned to the right dock.</summary>
    public double RightDockRatio { get; init; } = 0.22d;

    /// <summary>Gets the Work Area height assigned to the bottom dock.</summary>
    public double BottomDockRatio { get; init; } = 0.26d;

    /// <summary>Gets the left dock height assigned to the upper region.</summary>
    public double LeftUpperRatio { get; init; } = 0.50d;

    /// <summary>Gets the right dock height assigned to the upper region.</summary>
    public double RightUpperRatio { get; init; } = 0.50d;

    /// <summary>Gets the bottom dock width assigned to the left region.</summary>
    public double BottomLeftRatio { get; init; } = 0.50d;

    /// <summary>Gets whether the complete left dock is collapsed.</summary>
    public bool LeftDockCollapsed { get; init; }
    /// <summary>Gets whether the complete right dock is collapsed.</summary>
    public bool RightDockCollapsed { get; init; }
    /// <summary>Gets whether the complete bottom dock is collapsed.</summary>
    public bool BottomDockCollapsed { get; init; }
    /// <summary>Gets whether the lower left-dock region is collapsed.</summary>
    public bool LeftLowerCollapsed { get; init; }
    /// <summary>Gets whether the lower right-dock region is collapsed.</summary>
    public bool RightLowerCollapsed { get; init; }
    /// <summary>Gets whether the right bottom-dock region is collapsed.</summary>
    public bool BottomRightCollapsed { get; init; }
}

/// <summary>Describes a top-level layout splitter resize request.</summary>
public sealed record LayoutSplitterResizeRequest
{
    /// <summary>Gets the stable splitter identifier.</summary>
    public required string SplitterId { get; init; }

    /// <summary>Gets the normalized ratio associated with the splitter.</summary>
    public double Ratio { get; init; }
}
