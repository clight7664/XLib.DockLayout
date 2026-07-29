using System.Reflection;
using XLib.DockLayout.Abstractions;
using XLib.DockLayout.Models;

namespace XLib.DockLayout.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void RuntimeAssemblyHasExpectedIdentityAndNoThirdPartyDockDependency()
    {
        var assembly = typeof(IDockLayoutHandle).Assembly;
        Assert.Equal("XLib.DockLayout", assembly.GetName().Name);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference =>
            reference.Name is not null && (reference.Name.Contains("Dock", StringComparison.OrdinalIgnoreCase) || reference.Name.Contains("DragDrop", StringComparison.OrdinalIgnoreCase)) && reference.Name != "XLib.DockLayout");
    }

    [Fact]
    public void DomainConceptsRemainDistinctPublicTypes()
    {
        Assert.NotEqual(typeof(DockPanelDefinition), typeof(EditorViewDefinition));
        Assert.NotEqual(typeof(EditorViewDefinition), typeof(EditorTabDefinition));
        Assert.NotEqual(typeof(DockRegionName), typeof(DockPanelDefinition));
    }

    [Fact]
    public void PublicHandleSupportsRequiredXmlAndLifecycleOperations()
    {
        var names = typeof(IDockLayoutHandle).GetMethods().Select(method => method.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("ExportLayoutXmlAsync", names);
        Assert.Contains("ImportLayoutXmlAsync", names);
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IDockLayoutHandle)));
        Assert.Contains("MovePanelAsync", names);
        Assert.Contains("MoveTabAsync", names);
    }
}
