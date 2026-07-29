using Microsoft.Playwright;

namespace DockLayout.Playground.E2E;

public sealed class PlaygroundTests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private string? _url;

    public async Task InitializeAsync()
    {
        _url = Environment.GetEnvironmentVariable("PLAYGROUND_URL");
        if (string.IsNullOrWhiteSpace(_url)) return;
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
        _page = await _browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 1440, Height = 1000 } });
        await _page.GotoAsync(_url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    [Fact]
    public async Task OpenSwitchCloseAndExportLayout()
    {
        if (_page is null) return;
        await _page.GetByTestId("open-tab").ClickAsync();
        await Assertions.Expect(_page.Locator("[data-xdl-tab]")).ToHaveCountAsync(4);
        await _page.GetByTestId("export-layout").ClickAsync();
        await Assertions.Expect(_page.GetByTestId("layout-xml")).ToContainTextAsync("XLibDockLayout");
        await _page.Locator("[data-xdl-tab]").Last.GetByLabel("Close Generated-1.cs").ClickAsync();
        await Assertions.Expect(_page.Locator("[data-xdl-tab]")).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task MovePanelAndSplitViewUpdateSnapshot()
    {
        if (_page is null) return;
        await _page.GetByTestId("move-panel").ClickAsync();
        await Assertions.Expect(_page.Locator("[data-xdl-region=RightDockUpper] [data-xdl-panel=explorer]")).ToHaveCountAsync(1);
        await _page.GetByTestId("split-view").ClickAsync();
        await Assertions.Expect(_page.Locator("[data-xdl-editor-view]")).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task TwoScenarioInstancesRemainIndependent()
    {
        if (_page is null || _url is null) return;
        await _page.GotoAsync(new Uri(new Uri(_url), "scenarios").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Assertions.Expect(_page.Locator(".xdl-layout")).ToHaveCountAsync(2);
        var instanceIds = await _page.Locator(".xdl-layout").EvaluateAllAsync<string[]>("elements => elements.map(element => element.dataset.xdlInstance)");
        Assert.Equal(2, instanceIds.Distinct(StringComparer.Ordinal).Count());
    }


    [Fact]
    public async Task StandaloneTabsMenusThemeAndExplorerRemainInteractive()
    {
        if (_page is null || _url is null) return;
        await _page.GotoAsync(new Uri(new Uri(_url), "standalone").ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await _page.Locator("[data-xdl-tab='tab-welcome']").ClickAsync();
        await Assertions.Expect(_page.Locator("[data-xdl-tab='tab-welcome']")).ToHaveClassAsync(new Regex("xdl-active"));
        await _page.Locator("[data-xdl-tab='tab-readme']").ClickAsync();
        await Assertions.Expect(_page.Locator("[data-xdl-tab='tab-readme']")).ToHaveClassAsync(new Regex("xdl-active"));

        await _page.Locator(".demo-rider-menu-toggle").ClickAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "View" }).ClickAsync();
        await Assertions.Expect(_page.Locator("#docklayout-menu-view")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator(".demo-rider-popup")).ToHaveCountAsync(1);

        await _page.GetByRole(AriaRole.Button, new() { Name = "Appearance" }).ClickAsync();
        await Assertions.Expect(_page.Locator("#docklayout-menu-view")).ToHaveCountAsync(0);
        await Assertions.Expect(_page.Locator("#docklayout-menu-appearance")).ToBeVisibleAsync();
        await _page.Locator("#docklayout-menu-appearance [role=\"menuitemradio\"]").Nth(1).ClickAsync();
        await Assertions.Expect(_page.Locator(".xdl-layout")).ToHaveClassAsync(new Regex("xdl-theme-light"));

        var initialTabCount = await _page.Locator("[data-xdl-tab]").CountAsync();
        await _page.Locator("[data-demo-tree-item='file-store']").DblClickAsync();
        await Assertions.Expect(_page.Locator("[data-xdl-tab]")).ToHaveCountAsync(initialTabCount + 1);
        await _page.Locator("[data-demo-tree-item='file-store']").DblClickAsync();
        await Assertions.Expect(_page.Locator("[data-xdl-tab]")).ToHaveCountAsync(initialTabCount + 1);
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }
}
