using Microsoft.AspNetCore.Components;
using XLib.DockLayout.Abstractions;
using XLib.DockLayout.Models;

namespace XLib.DockLayout.Services;

/// <summary>Thread-safe in-process content registry.</summary>
public sealed class DockContentRegistry : IDockContentRegistry
{
    private readonly Dictionary<string, Type> _types = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    /// <inheritdoc />
    public DockLayoutResult Register<TComponent>(string contentId) where TComponent : IComponent => Register(contentId, typeof(TComponent));

    /// <inheritdoc />
    public DockLayoutResult Register(string contentId, Type componentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        ArgumentNullException.ThrowIfNull(componentType);
        if (!typeof(IComponent).IsAssignableFrom(componentType))
        {
            return DockLayoutResult.Fail("INVALID_CONTENT_TYPE", $"Type '{componentType}' is not a Blazor component.");
        }

        lock (_sync)
        {
            if (_types.ContainsKey(contentId))
            {
                return DockLayoutResult.Fail("DUPLICATE_CONTENT_KEY", $"Content ID '{contentId}' is already registered.");
            }
            _types.Add(contentId, componentType);
        }
        return DockLayoutResult.Ok();
    }

    /// <inheritdoc />
    public bool TryResolve(string contentId, out Type componentType)
    {
        lock (_sync)
        {
            return _types.TryGetValue(contentId, out componentType!);
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetRegisteredKeys()
    {
        lock (_sync)
        {
            return _types.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
        }
    }
}
