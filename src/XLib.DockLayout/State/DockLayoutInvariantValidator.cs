using XLib.DockLayout.Models;

namespace XLib.DockLayout.State;

internal static class DockLayoutInvariantValidator
{
    private const double RatioTolerance = 0.000001d;

    public static DockLayoutResult Validate(DockLayoutSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Panels.Select(panel => panel.Id).Distinct(StringComparer.Ordinal).Count() != snapshot.Panels.Count)
        {
            return DockLayoutResult.Fail("DUPLICATE_PANEL_ID", "DockPanel IDs must be globally unique.");
        }

        if (snapshot.EditorViews.Select(view => view.Id).Distinct(StringComparer.Ordinal).Count() != snapshot.EditorViews.Count)
        {
            return DockLayoutResult.Fail("DUPLICATE_VIEW_ID", "EditorView IDs must be globally unique.");
        }

        var tabs = snapshot.EditorViews.SelectMany(view => view.Tabs).ToArray();
        if (tabs.Select(tab => tab.Id).Distinct(StringComparer.Ordinal).Count() != tabs.Length)
        {
            return DockLayoutResult.Fail("DUPLICATE_TAB_ID", "EditorTab IDs must be globally unique.");
        }

        foreach (var view in snapshot.EditorViews)
        {
            if (view.Tabs.Any(tab => !string.Equals(tab.ViewId, view.Id, StringComparison.Ordinal)))
            {
                return DockLayoutResult.Fail("TAB_OWNERSHIP_INVALID", $"EditorView '{view.Id}' contains a tab owned by another view.");
            }

            if (view.Tabs.Count(tab => tab.Active) > 1)
            {
                return DockLayoutResult.Fail("MULTIPLE_ACTIVE_TABS", $"EditorView '{view.Id}' has multiple active tabs.");
            }

            if (view.ActiveTabId is not null && view.Tabs.All(tab => tab.Id != view.ActiveTabId))
            {
                return DockLayoutResult.Fail("ACTIVE_TAB_NOT_FOUND", $"EditorView '{view.Id}' references an unknown active tab.");
            }

            if (view.ActiveTabId is null && view.Tabs.Any(tab => tab.Active))
            {
                return DockLayoutResult.Fail("ACTIVE_TAB_STATE_INVALID", $"EditorView '{view.Id}' has an active tab without ActiveTabId.");
            }

            var firstUnpinned = view.Tabs.ToList().FindIndex(tab => !tab.Pinned);
            if (firstUnpinned >= 0 && view.Tabs.Skip(firstUnpinned).Any(tab => tab.Pinned))
            {
                return DockLayoutResult.Fail("PINNED_ORDER_INVALID", $"EditorView '{view.Id}' does not keep pinned tabs first.");
            }
        }

        foreach (var region in Enum.GetValues<DockRegionName>())
        {
            if (snapshot.Panels.Count(panel => panel.Region == region && panel.Active && panel.Visible && !panel.Collapsed) > 1)
            {
                return DockLayoutResult.Fail("MULTIPLE_ACTIVE_PANELS", $"Dock Region '{region}' has multiple active panels.");
            }
        }

        if (snapshot.Panels.Any(panel => !IsValidRatio(panel.Ratio)) || snapshot.EditorViews.Any(view => !IsValidRatio(view.Ratio)))
        {
            return DockLayoutResult.Fail("INVALID_RATIO", "All layout ratios must be finite and non-negative.");
        }

        if (snapshot.Panels.Any(panel => panel.MinSize < 0d || panel.MaxSize < panel.MinSize))
        {
            return DockLayoutResult.Fail("INVALID_PANEL_SIZE", "DockPanel minimum and maximum sizes are inconsistent.");
        }

        var geometryRatios = new[]
        {
            snapshot.Geometry.LeftDockRatio,
            snapshot.Geometry.RightDockRatio,
            snapshot.Geometry.BottomDockRatio,
            snapshot.Geometry.LeftUpperRatio,
            snapshot.Geometry.RightUpperRatio,
            snapshot.Geometry.BottomLeftRatio
        };
        if (geometryRatios.Any(ratio => !IsFinite(ratio) || ratio < 0d || ratio > 1d))
        {
            return DockLayoutResult.Fail("INVALID_GEOMETRY_RATIO", "Top-level layout geometry ratios must be finite values between zero and one.");
        }
        if (snapshot.Geometry.LeftDockRatio + snapshot.Geometry.RightDockRatio > 0.90d + RatioTolerance)
        {
            return DockLayoutResult.Fail("INVALID_GEOMETRY_RATIO", "Left and right dock ratios leave insufficient Editor Area width.");
        }

        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        var leafViewIds = new List<string>();
        var treeValidation = ValidateTree(snapshot.EditorRoot, nodeIds, leafViewIds);
        if (!treeValidation.Success) return treeValidation;

        if (leafViewIds.Distinct(StringComparer.Ordinal).Count() != leafViewIds.Count)
        {
            return DockLayoutResult.Fail("DUPLICATE_VIEW_LEAF", "Each EditorView must occur exactly once in the EditorArea split tree.");
        }

        var declaredViews = snapshot.EditorViews.Select(view => view.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var treeViews = leafViewIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (!declaredViews.SequenceEqual(treeViews, StringComparer.Ordinal))
        {
            return DockLayoutResult.Fail("EDITOR_TREE_VIEW_MISMATCH", "EditorArea split tree leaves must exactly match the EditorView collection.");
        }

        return DockLayoutResult.Ok();
    }

    private static DockLayoutResult ValidateTree(EditorNodeInfo node, ISet<string> nodeIds, ICollection<string> leafViewIds)
    {
        if (string.IsNullOrWhiteSpace(node.Id))
        {
            return DockLayoutResult.Fail("INVALID_EDITOR_NODE_ID", "EditorArea split-tree node IDs are required.");
        }

        if (!nodeIds.Add(node.Id))
        {
            return DockLayoutResult.Fail("DUPLICATE_EDITOR_NODE_ID", $"EditorArea node ID '{node.Id}' is duplicated.");
        }

        switch (node)
        {
            case EditorLeafInfo leaf:
                if (string.IsNullOrWhiteSpace(leaf.ViewId))
                {
                    return DockLayoutResult.Fail("INVALID_EDITOR_LEAF", $"Editor leaf '{leaf.Id}' must reference a view.");
                }
                leafViewIds.Add(leaf.ViewId);
                return DockLayoutResult.Ok();

            case EditorSplitInfo split:
                if (!IsFinite(split.FirstRatio) || split.FirstRatio < 0.05d - RatioTolerance || split.FirstRatio > 0.95d + RatioTolerance)
                {
                    return DockLayoutResult.Fail("INVALID_SPLIT_RATIO", $"Editor split '{split.Id}' ratio must be between 0.05 and 0.95.");
                }
                var first = ValidateTree(split.First, nodeIds, leafViewIds);
                return first.Success ? ValidateTree(split.Second, nodeIds, leafViewIds) : first;

            default:
                return DockLayoutResult.Fail("INVALID_EDITOR_NODE", "EditorArea split tree contains an unsupported node type.");
        }
    }

    private static bool IsValidRatio(double ratio) => IsFinite(ratio) && ratio >= -RatioTolerance;

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
