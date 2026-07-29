using DockLayout.Playground;
using DockLayout.Playground.Components.Content;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using XLib.DockLayout.Abstractions;
using XLib.DockLayout.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddXLibDockLayout();

var host = builder.Build();
var registry = host.Services.GetRequiredService<IDockContentRegistry>();
Register<ExplorerContent>(registry, "explorer");
Register<SearchContent>(registry, "search");
Register<OutlineContent>(registry, "outline");
Register<PropertiesContent>(registry, "properties");
Register<TerminalContent>(registry, "terminal");
Register<ProblemsContent>(registry, "problems");
Register<RelationTableContent>(registry, "relations");
Register<OutputContent>(registry, "output");
Register<TextEditorContent>(registry, "text-editor");
Register<WelcomeContent>(registry, "welcome");

await host.RunAsync();

static void Register<TComponent>(IDockContentRegistry registry, string contentId)
    where TComponent : Microsoft.AspNetCore.Components.IComponent
{
    var result = registry.Register<TComponent>(contentId);
    if (!result.Success) throw new InvalidOperationException(result.Error?.Message);
}
