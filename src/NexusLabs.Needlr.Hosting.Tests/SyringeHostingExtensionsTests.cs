using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Hosting.Tests;

public sealed class SyringeHostingExtensionsTests
{
    [Fact]
    public void ForHost_ReturnsHostSyringe()
    {
        var syringe = new Syringe().UsingReflection();
        var hostSyringe = syringe.ForHost();

        Assert.NotNull(hostSyringe);
        Assert.IsType<HostSyringe>(hostSyringe);
    }

    [Fact]
    public void ForHost_WithNullSyringe_ThrowsArgumentNullException()
    {
        ConfiguredSyringe? syringe = null;
        Assert.Throws<ArgumentNullException>(() => syringe!.ForHost());
    }

    [Fact]
    public void BuildHost_WithNullSyringe_ThrowsArgumentNullException()
    {
        ConfiguredSyringe? syringe = null;
        Assert.Throws<ArgumentNullException>(() => syringe!.BuildHost());
    }

    [Fact]
    public void ForHost_ChainedFromReflection_WorksCorrectly()
    {
        var hostSyringe = new Syringe()
            .UsingReflection()
            .ForHost();

        Assert.NotNull(hostSyringe);
    }
}
