using Microsoft.Extensions.DependencyInjection;
using XLib.DockLayout.Abstractions;
using XLib.DockLayout.Services;

namespace XLib.DockLayout.DependencyInjection;

/// <summary>Registers XLib.DockLayout runtime services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds XLib.DockLayout services to a Blazor application.</summary>
    public static IServiceCollection AddXLibDockLayout(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IDockContentRegistry, DockContentRegistry>();
        services.AddScoped<IDockLayoutSerializer, DockLayoutSerializer>();
        services.AddScoped<IDockLayoutPersistence, BrowserDockLayoutPersistence>();
        return services;
    }
}
