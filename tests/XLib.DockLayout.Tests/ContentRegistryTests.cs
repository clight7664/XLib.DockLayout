using XLib.DockLayout.Services;

namespace XLib.DockLayout.Tests;

public sealed class ContentRegistryTests
{
    [Fact]
    public void RegisterAndResolveComponent()
    {
        var registry = new DockContentRegistry();
        var result = registry.Register<TestContent>("test");
        Assert.True(result.Success);
        Assert.True(registry.TryResolve("test", out var type));
        Assert.Equal(typeof(TestContent), type);
    }

    [Fact]
    public void DuplicateRegistrationReturnsExpectedError()
    {
        var registry = new DockContentRegistry();
        Assert.True(registry.Register<TestContent>("test").Success);
        var duplicate = registry.Register<TestContent>("test");
        Assert.False(duplicate.Success);
        Assert.Equal("DUPLICATE_CONTENT_KEY", duplicate.Error?.Code);
    }
}
