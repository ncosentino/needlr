using NexusLabs.Needlr.Injection;

using Xunit;

namespace NexusLabs.Needlr.Hosting.Tests;

public sealed class SyringeHostingExtensionsTests
{
    [Fact]
    public void ForHost_ReturnsHostSyringe()
    {
        var syringe = new Syringe();
        var hostSyringe = syringe.ForHost();

        Assert.NotNull(hostSyringe);
        Assert.IsType<HostSyringe>(hostSyringe);
    }

    [Fact]
    public void ForHost_WithNullSyringe_ThrowsArgumentNullException()
    {
        Syringe? syringe = null;
        Assert.Throws<ArgumentNullException>(() => syringe!.ForHost());
    }

    [Fact]
    public void BuildHost_WithNullSyringe_ThrowsArgumentNullException()
    {
        Syringe? syringe = null;
        Assert.Throws<ArgumentNullException>(() => syringe!.BuildHost());
    }

    [Fact]
    public void ForHost_ChainedFromReflection_WorksCorrectly()
    {
        var hostSyringe = new Syringe()
            .ForHost();

        Assert.NotNull(hostSyringe);
    }
}
