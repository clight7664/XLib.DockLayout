using Microsoft.AspNetCore.Components;
using XLib.DockLayout.Models;

namespace XLib.DockLayout.Abstractions;

/// <summary>Maps stable content IDs to Blazor component types.</summary>
public interface IDockContentRegistry
{
    /// <summary>Registers a component type for a content ID.</summary>
    DockLayoutResult Register<TComponent>(string contentId) where TComponent : IComponent;
    /// <summary>Registers a component type for a content ID.</summary>
    DockLayoutResult Register(string contentId, Type componentType);
    /// <summary>Resolves a registered component type.</summary>
    bool TryResolve(string contentId, out Type componentType);
    /// <summary>Returns all registered keys.</summary>
    IReadOnlyCollection<string> GetRegisteredKeys();
}
