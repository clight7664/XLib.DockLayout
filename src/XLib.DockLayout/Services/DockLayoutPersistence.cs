using System.Collections.Concurrent;
using Microsoft.JSInterop;
using XLib.DockLayout.Abstractions;

namespace XLib.DockLayout.Services;

/// <summary>Browser localStorage persistence with an in-memory degradation path.</summary>
public sealed class BrowserDockLayoutPersistence : IDockLayoutPersistence
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ConcurrentDictionary<string, string> _fallback = new(StringComparer.Ordinal);

    /// <summary>Creates browser persistence using the supplied JS runtime.</summary>
    public BrowserDockLayoutPersistence(IJSRuntime jsRuntime)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(string key, string xml, CancellationToken cancellationToken = default)
    {
        _fallback[key] = xml;
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, key, xml).ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            // Disposal/navigation can disconnect the browser runtime.
        }
        catch (JSException)
        {
            // The in-memory copy remains authoritative for this process.
        }
    }

    /// <inheritdoc />
    public async ValueTask<string?> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, key).ConfigureAwait(false);
            if (value is not null) _fallback[key] = value;
            return value ?? (_fallback.TryGetValue(key, out var fallback) ? fallback : null);
        }
        catch (JSDisconnectedException)
        {
            return _fallback.TryGetValue(key, out var fallback) ? fallback : null;
        }
        catch (JSException)
        {
            return _fallback.TryGetValue(key, out var fallback) ? fallback : null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _fallback.TryRemove(key, out _);
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, key).ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
    }
}
