using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using XLib.DockLayout.Abstractions;
using XLib.DockLayout.Models;
using XLib.DockLayout.State;

namespace XLib.DockLayout.Services;

/// <summary>Provides secure, atomic XML version 1 serialization for DockLayout snapshots.</summary>
public sealed class DockLayoutSerializer : IDockLayoutSerializer
{
    private const int MaxXmlCharacters = 1_048_576;
    private const int MaxNodes = 5_000;
    private const int MaxDepth = 32;
    private const int MaxParameterCharacters = 65_536;
    private const int MaxTextCharacters = 4_096;

    /// <inheritdoc />
    public DockLayoutResult<string> Serialize(DockLayoutSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var validation = DockLayoutInvariantValidator.Validate(snapshot);
        if (!validation.Success) return DockLayoutResult<string>.Fail(validation.Error!.Code, validation.Error.Message);

        try
        {
            var root = new XElement("XLibDockLayout",
                new XAttribute("version", snapshot.Version),
                new XAttribute("instanceId", snapshot.InstanceId),
                new XAttribute("revision", snapshot.Revision));

            var regions = new XElement("Regions");
            foreach (var region in Enum.GetValues<DockRegionName>())
            {
                var regionElement = new XElement("Region", new XAttribute("name", region));
                foreach (var panel in snapshot.Panels.Where(item => item.Region == region).OrderBy(item => item.Order))
                {
                    regionElement.Add(SerializePanel(panel));
                }
                regions.Add(regionElement);
            }
            root.Add(regions);
            root.Add(new XElement("Geometry",
                new XAttribute("leftDockRatio", snapshot.Geometry.LeftDockRatio.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("rightDockRatio", snapshot.Geometry.RightDockRatio.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("bottomDockRatio", snapshot.Geometry.BottomDockRatio.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("leftUpperRatio", snapshot.Geometry.LeftUpperRatio.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("rightUpperRatio", snapshot.Geometry.RightUpperRatio.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("bottomLeftRatio", snapshot.Geometry.BottomLeftRatio.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("leftDockCollapsed", snapshot.Geometry.LeftDockCollapsed),
                new XAttribute("rightDockCollapsed", snapshot.Geometry.RightDockCollapsed),
                new XAttribute("bottomDockCollapsed", snapshot.Geometry.BottomDockCollapsed),
                new XAttribute("leftLowerCollapsed", snapshot.Geometry.LeftLowerCollapsed),
                new XAttribute("rightLowerCollapsed", snapshot.Geometry.RightLowerCollapsed),
                new XAttribute("bottomRightCollapsed", snapshot.Geometry.BottomRightCollapsed)));

            var viewLookup = snapshot.EditorViews.ToDictionary(view => view.Id, StringComparer.Ordinal);
            root.Add(new XElement("EditorArea", SerializeEditorNode(snapshot.EditorRoot, viewLookup)));

            var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            var xml = document.ToString(SaveOptions.DisableFormatting);
            return xml.Length <= MaxXmlCharacters
                ? DockLayoutResult<string>.Ok(xml)
                : DockLayoutResult<string>.Fail("LAYOUT_SERIALIZATION_FAILED", "Serialized layout exceeds the configured XML size limit.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or ArgumentException)
        {
            return DockLayoutResult<string>.Fail("LAYOUT_SERIALIZATION_FAILED", exception.Message);
        }
    }

    /// <inheritdoc />
    public DockLayoutResult<DockLayoutSnapshot> Deserialize(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return DockLayoutResult<DockLayoutSnapshot>.Fail("INVALID_LAYOUT_DATA", "Layout XML is empty.");
        if (xml.Length > MaxXmlCharacters) return DockLayoutResult<DockLayoutSnapshot>.Fail("INVALID_LAYOUT_DATA", "Layout XML exceeds the configured size limit.");

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxXmlCharacters,
                MaxCharactersFromEntities = 0,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            };
            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, settings);
            var document = XDocument.Load(reader, LoadOptions.None);
            var root = document.Root;
            if (root?.Name.LocalName != "XLibDockLayout")
            {
                return DockLayoutResult<DockLayoutSnapshot>.Fail("INVALID_LAYOUT_DATA", "The root element must be XLibDockLayout.");
            }

            var allNodes = root.DescendantsAndSelf().ToArray();
            if (allNodes.Length > MaxNodes || allNodes.Max(node => node.Ancestors().Count()) > MaxDepth)
            {
                return DockLayoutResult<DockLayoutSnapshot>.Fail("INVALID_LAYOUT_DATA", "Layout XML exceeds node or depth limits.");
            }

            var version = Required(root, "version");
            if (!Version.TryParse(version, out var parsedVersion) || parsedVersion is null || parsedVersion.Major != 1)
            {
                return DockLayoutResult<DockLayoutSnapshot>.Fail("LAYOUT_VERSION_INCOMPATIBLE", $"Layout version '{version}' is incompatible with major version 1.");
            }

            var panels = ParsePanels(root);
            var geometryElement = root.Element("Geometry");
            var geometry = geometryElement is null ? new DockLayoutGeometry() : new DockLayoutGeometry
            {
                LeftDockRatio = ParseDouble(Attribute(geometryElement, "leftDockRatio", "0.20")),
                RightDockRatio = ParseDouble(Attribute(geometryElement, "rightDockRatio", "0.22")),
                BottomDockRatio = ParseDouble(Attribute(geometryElement, "bottomDockRatio", "0.26")),
                LeftUpperRatio = ParseDouble(Attribute(geometryElement, "leftUpperRatio", "0.50")),
                RightUpperRatio = ParseDouble(Attribute(geometryElement, "rightUpperRatio", "0.50")),
                BottomLeftRatio = ParseDouble(Attribute(geometryElement, "bottomLeftRatio", "0.50")),
                LeftDockCollapsed = ParseBool(Attribute(geometryElement, "leftDockCollapsed", "false")),
                RightDockCollapsed = ParseBool(Attribute(geometryElement, "rightDockCollapsed", "false")),
                BottomDockCollapsed = ParseBool(Attribute(geometryElement, "bottomDockCollapsed", "false")),
                LeftLowerCollapsed = ParseBool(Attribute(geometryElement, "leftLowerCollapsed", "false")),
                RightLowerCollapsed = ParseBool(Attribute(geometryElement, "rightLowerCollapsed", "false")),
                BottomRightCollapsed = ParseBool(Attribute(geometryElement, "bottomRightCollapsed", "false"))
            };
            var editorArea = root.Element("EditorArea");
            var views = new List<EditorViewInfo>();
            EditorNodeInfo editorRoot;

            var editorElements = editorArea?.Elements()
                .Where(element => element.Name.LocalName is "Split" or "View")
                .ToArray() ?? Array.Empty<XElement>();

            if (editorElements.Length == 0)
            {
                var defaultView = CreateDefaultView();
                views.Add(defaultView);
                editorRoot = new EditorLeafInfo { Id = "leaf-view-main", ViewId = defaultView.Id };
            }
            else if (editorElements.Length == 1)
            {
                editorRoot = ParseEditorNode(editorElements[0], views);
            }
            else
            {
                // Backward-compatible v1 flat format: multiple View elements directly under EditorArea.
                var leaves = editorElements.Select(element => ParseEditorNode(element, views)).ToArray();
                editorRoot = BuildImportedTree(leaves);
            }

            var snapshot = new DockLayoutSnapshot
            {
                Version = version,
                InstanceId = Attribute(root, "instanceId", "imported"),
                Revision = long.TryParse(Attribute(root, "revision", "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var revision) ? revision : 0,
                Panels = panels,
                EditorViews = views.OrderBy(view => view.Order).ToArray(),
                EditorRoot = editorRoot,
                Geometry = geometry
            };

            var validation = DockLayoutInvariantValidator.Validate(snapshot);
            return validation.Success
                ? DockLayoutResult<DockLayoutSnapshot>.Ok(snapshot)
                : DockLayoutResult<DockLayoutSnapshot>.Fail("INVALID_LAYOUT_DATA", validation.Error!.Message);
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException or FormatException or JsonException or ArgumentException)
        {
            return DockLayoutResult<DockLayoutSnapshot>.Fail("INVALID_LAYOUT_DATA", exception.Message);
        }
    }

    private static XElement SerializePanel(DockPanelInfo panel) => new("Panel",
        new XAttribute("id", panel.Id),
        new XAttribute("contentId", panel.ContentId ?? string.Empty),
        new XAttribute("title", panel.Title),
        new XAttribute("icon", panel.Icon ?? string.Empty),
        new XAttribute("lifecycle", panel.Lifecycle),
        new XAttribute("ratio", panel.Ratio.ToString("R", CultureInfo.InvariantCulture)),
        new XAttribute("minSize", panel.MinSize.ToString("R", CultureInfo.InvariantCulture)),
        new XAttribute("maxSize", panel.MaxSize.ToString("R", CultureInfo.InvariantCulture)),
        new XAttribute("visible", panel.Visible),
        new XAttribute("toolBarVisible", panel.ToolBarVisible),
        new XAttribute("collapsed", panel.Collapsed),
        new XAttribute("active", panel.Active),
        new XAttribute("autoHide", panel.AutoHide),
        new XAttribute("order", panel.Order),
        SerializeParameters(panel.Parameters));

    private static XElement SerializeEditorNode(EditorNodeInfo node, IReadOnlyDictionary<string, EditorViewInfo> views) => node switch
    {
        EditorLeafInfo leaf => SerializeView(leaf, views.TryGetValue(leaf.ViewId, out var view)
            ? view
            : throw new InvalidOperationException($"Editor leaf '{leaf.Id}' references unknown view '{leaf.ViewId}'.")),
        EditorSplitInfo split => new XElement("Split",
            new XAttribute("id", split.Id),
            new XAttribute("orientation", split.Orientation),
            new XAttribute("firstRatio", split.FirstRatio.ToString("R", CultureInfo.InvariantCulture)),
            new XAttribute("collapsedSide", split.CollapsedSide),
            SerializeEditorNode(split.First, views),
            SerializeEditorNode(split.Second, views)),
        _ => throw new InvalidOperationException("Unsupported EditorArea split-tree node type.")
    };

    private static XElement SerializeView(EditorLeafInfo leaf, EditorViewInfo view)
    {
        var viewElement = new XElement("View",
            new XAttribute("nodeId", leaf.Id),
            new XAttribute("id", view.Id),
            new XAttribute("lifecycle", view.Lifecycle),
            new XAttribute("activeTabId", view.ActiveTabId ?? string.Empty),
            new XAttribute("ratio", view.Ratio.ToString("R", CultureInfo.InvariantCulture)),
            new XAttribute("orientation", view.Orientation),
            new XAttribute("order", view.Order));

        foreach (var tab in view.Tabs.OrderBy(item => item.Order))
        {
            viewElement.Add(new XElement("Tab",
                new XAttribute("id", tab.Id),
                new XAttribute("contentId", tab.ContentId),
                new XAttribute("title", tab.Title),
                new XAttribute("icon", tab.Icon ?? string.Empty),
                new XAttribute("closable", tab.Closable),
                new XAttribute("pinned", tab.Pinned),
                new XAttribute("active", tab.Active),
                new XAttribute("order", tab.Order),
                new XAttribute("dedupParameter", tab.DedupParameter ?? string.Empty),
                SerializeParameters(tab.Parameters)));
        }
        return viewElement;
    }

    private static XElement SerializeParameters(IReadOnlyDictionary<string, object?> parameters)
    {
        var json = JsonSerializer.Serialize(parameters);
        if (json.Length > MaxParameterCharacters) throw new InvalidOperationException("Parameters exceed the configured size limit.");
        return new XElement("Parameters", json);
    }

    private static IReadOnlyList<DockPanelInfo> ParsePanels(XElement root)
    {
        var panels = new List<DockPanelInfo>();
        foreach (var regionElement in root.Element("Regions")?.Elements("Region") ?? Enumerable.Empty<XElement>())
        {
            var region = ParseEnum<DockRegionName>(Required(regionElement, "name"));
            foreach (var panelElement in regionElement.Elements("Panel"))
            {
                panels.Add(new DockPanelInfo
                {
                    Id = RequiredLimited(panelElement, "id"),
                    Region = region,
                    ContentId = EmptyToNull(Limited(Attribute(panelElement, "contentId"))),
                    Title = RequiredLimited(panelElement, "title"),
                    Icon = EmptyToNull(Limited(Attribute(panelElement, "icon"))),
                    Lifecycle = ParseEnum<DockPanelLifecycle>(Attribute(panelElement, "lifecycle", "Template")),
                    Ratio = ParseDouble(Attribute(panelElement, "ratio", "1")),
                    MinSize = ParseDouble(Attribute(panelElement, "minSize", "120")),
                    MaxSize = ParseDouble(Attribute(panelElement, "maxSize", "1200")),
                    Visible = ParseBool(Attribute(panelElement, "visible", "true")),
                    ToolBarVisible = ParseBool(Attribute(panelElement, "toolBarVisible", "true")),
                    Collapsed = ParseBool(Attribute(panelElement, "collapsed", "false")),
                    Active = ParseBool(Attribute(panelElement, "active", "false")),
                    AutoHide = ParseBool(Attribute(panelElement, "autoHide", "false")),
                    Order = ParseInt(Attribute(panelElement, "order", "0")),
                    Parameters = ParseParameters(panelElement.Element("Parameters")?.Value)
                });
            }
        }
        return panels;
    }

    private static EditorNodeInfo ParseEditorNode(XElement element, ICollection<EditorViewInfo> views)
    {
        if (element.Name.LocalName == "View")
        {
            var view = ParseView(element);
            views.Add(view);
            return new EditorLeafInfo
            {
                Id = Limited(Attribute(element, "nodeId", $"leaf-{view.Id}")),
                ViewId = view.Id
            };
        }

        if (element.Name.LocalName != "Split") throw new FormatException($"Unsupported EditorArea element '{element.Name.LocalName}'.");
        var children = element.Elements().Where(child => child.Name.LocalName is "Split" or "View").ToArray();
        if (children.Length != 2) throw new FormatException("Each EditorArea Split must contain exactly two View/Split children.");
        return new EditorSplitInfo
        {
            Id = RequiredLimited(element, "id"),
            Orientation = ParseEnum<SplitOrientation>(Attribute(element, "orientation", "Vertical")),
            FirstRatio = ParseDouble(Attribute(element, "firstRatio", Attribute(element, "ratio", "0.5"))),
            CollapsedSide = ParseEnum<SplitCollapsedSide>(Attribute(element, "collapsedSide", "None")),
            First = ParseEditorNode(children[0], views),
            Second = ParseEditorNode(children[1], views)
        };
    }

    private static EditorViewInfo ParseView(XElement viewElement)
    {
        var viewId = RequiredLimited(viewElement, "id");
        var tabs = new List<EditorTabInfo>();
        foreach (var tabElement in viewElement.Elements("Tab"))
        {
            tabs.Add(new EditorTabInfo
            {
                Id = RequiredLimited(tabElement, "id"),
                ViewId = viewId,
                ContentId = RequiredLimited(tabElement, "contentId"),
                Title = RequiredLimited(tabElement, "title"),
                Icon = EmptyToNull(Limited(Attribute(tabElement, "icon"))),
                Closable = ParseBool(Attribute(tabElement, "closable", "true")),
                Pinned = ParseBool(Attribute(tabElement, "pinned", "false")),
                Active = ParseBool(Attribute(tabElement, "active", "false")),
                Order = ParseInt(Attribute(tabElement, "order", "0")),
                DedupParameter = EmptyToNull(Limited(Attribute(tabElement, "dedupParameter"))),
                Parameters = ParseParameters(tabElement.Element("Parameters")?.Value)
            });
        }

        var orderedTabs = tabs.OrderBy(tab => tab.Order).ToArray();
        var declaredActive = EmptyToNull(Attribute(viewElement, "activeTabId"));
        var activeId = declaredActive ?? orderedTabs.FirstOrDefault(tab => tab.Active)?.Id ?? orderedTabs.FirstOrDefault()?.Id;
        return new EditorViewInfo
        {
            Id = viewId,
            Lifecycle = ParseEnum<EditorViewLifecycle>(Attribute(viewElement, "lifecycle", "Template")),
            ActiveTabId = activeId,
            Ratio = ParseDouble(Attribute(viewElement, "ratio", "1")),
            Orientation = ParseEnum<SplitOrientation>(Attribute(viewElement, "orientation", "Vertical")),
            Order = ParseInt(Attribute(viewElement, "order", viewsOrderFallback(viewElement))),
            Tabs = orderedTabs.Select(tab => tab with { Active = tab.Id == activeId }).ToArray()
        };

        static string viewsOrderFallback(XElement value) => value.Parent?.Elements("View").TakeWhile(item => item != value).Count().ToString(CultureInfo.InvariantCulture) ?? "0";
    }

    private static EditorNodeInfo BuildImportedTree(IReadOnlyList<EditorNodeInfo> nodes)
    {
        if (nodes.Count == 0) return new EditorLeafInfo { Id = "leaf-view-main", ViewId = "view-main" };
        var root = nodes[0];
        for (var index = 1; index < nodes.Count; index++)
        {
            root = new EditorSplitInfo
            {
                Id = $"split-import-{index}",
                Orientation = SplitOrientation.Vertical,
                FirstRatio = 0.5d,
                First = root,
                Second = nodes[index]
            };
        }
        return root;
    }

    private static EditorViewInfo CreateDefaultView() => new()
    {
        Id = "view-main",
        Lifecycle = EditorViewLifecycle.Template,
        Ratio = 1d,
        Orientation = SplitOrientation.Vertical,
        Order = 0,
        Tabs = Array.Empty<EditorTabInfo>()
    };

    private static string Required(XElement element, string name) =>
        element.Attribute(name)?.Value is { Length: > 0 } value ? value : throw new FormatException($"Required attribute '{name}' is missing.");

    private static string RequiredLimited(XElement element, string name) => Limited(Required(element, name));

    private static string Attribute(XElement element, string name, string fallback = "") => element.Attribute(name)?.Value ?? fallback;

    private static string Limited(string value)
    {
        if (value.Length > MaxTextCharacters) throw new FormatException("A layout text value exceeds the configured size limit.");
        return value;
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static bool ParseBool(string value) => bool.Parse(value);
    private static int ParseInt(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    private static double ParseDouble(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static T ParseEnum<T>(string value) where T : struct, Enum => Enum.Parse<T>(value, true);

    private static IReadOnlyDictionary<string, object?> ParseParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object?>();
        if (json.Length > MaxParameterCharacters) throw new FormatException("Parameters exceed the configured size limit.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16, CommentHandling = JsonCommentHandling.Disallow });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new FormatException("Parameters must be a JSON object.");
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => ConvertJson(property.Value), StringComparer.Ordinal);
    }

    private static object? ConvertJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Object or JsonValueKind.Array => element.Clone(),
        _ => throw new FormatException("Unsupported parameter JSON value.")
    };
}
