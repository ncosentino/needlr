using NexusLabs.Needlr.AgentFramework.Providers;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class ProviderFailureContextTests
{
    [Fact]
    public void Constructor_StoresAllFields()
    {
        var ex = new InvalidOperationException("boom");
        var skipUntil = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var ctx = new ProviderFailureContext(
            ProviderName: "Premium",
            Exception: ex,
            SkipUntil: skipUntil);

        Assert.Equal("Premium", ctx.ProviderName);
        Assert.Same(ex, ctx.Exception);
        Assert.Equal(skipUntil, ctx.SkipUntil);
    }

    [Fact]
    public void Constructor_NullSkipUntil_IsAllowed()
    {
        var ex = new InvalidOperationException();

        var ctx = new ProviderFailureContext(
            ProviderName: "Premium",
            Exception: ex,
            SkipUntil: null);

        Assert.Null(ctx.SkipUntil);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var ex = new InvalidOperationException("boom");
        var skipUntil = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var a = new ProviderFailureContext("P", ex, skipUntil);
        var b = new ProviderFailureContext("P", ex, skipUntil);

        Assert.Equal(a, b);
    }
}
