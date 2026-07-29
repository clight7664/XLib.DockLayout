using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using XLib.DockLayout.Abstractions;

namespace XLib.DockLayout.Tests;

public sealed class TestContent : ComponentBase
{
    [Parameter] public string? Value { get; set; }
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "data-testid", "test-content");
        builder.AddContent(2, Value ?? "test");
        builder.CloseElement();
    }
}

internal sealed class MemoryPersistence : IDockLayoutPersistence
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    public ValueTask SaveAsync(string key, string xml, CancellationToken cancellationToken = default) { _values[key] = xml; return ValueTask.CompletedTask; }
    public ValueTask<string?> LoadAsync(string key, CancellationToken cancellationToken = default) => ValueTask.FromResult(_values.TryGetValue(key, out var value) ? value : null);
    public ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default) { _values.Remove(key); return ValueTask.CompletedTask; }
}
