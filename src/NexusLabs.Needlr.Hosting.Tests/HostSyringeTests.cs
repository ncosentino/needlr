using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Hosting.Tests;

public sealed class HostSyringeTests
{
    [Fact]
    public void DefaultConstructor_CreatesValidInstance()
    {
        var syringe = new HostSyringe(new Syringe().UsingReflection());
        Assert.NotNull(syringe);
    }

    [Fact]
    public void Constructor_WithBaseSyringe_WrapsCorrectly()
    {
        var baseSyringe = new Syringe().UsingReflection();
        var hostSyringe = new HostSyringe(baseSyringe);
        Assert.NotNull(hostSyringe);
    }

    [Fact]
    public void Constructor_WithNullBaseSyringe_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HostSyringe(null!));
    }

    [Fact]
    public void UsingOptions_ReturnsNewInstance()
    {
        var syringe = new HostSyringe(new Syringe().UsingReflection());
        var newSyringe = syringe.UsingOptions(() => CreateHostOptions.Default);

        Assert.NotSame(syringe, newSyringe);
    }

    [Fact]
    public void UsingConfigurationCallback_ReturnsNewInstance()
    {
        var syringe = new HostSyringe(new Syringe().UsingReflection());
        var newSyringe = syringe.UsingConfigurationCallback((builder, options) => { });

        Assert.NotSame(syringe, newSyringe);
    }

    [Fact]
    public void FluentChaining_WorksCorrectly()
    {
        var syringe = new Syringe()
            .UsingReflection()
            .ForHost()
            .UsingOptions(() => CreateHostOptions.Default
                .UsingApplicationName("TestApp")
                .UsingEnvironmentName("Development"))
            .UsingConfigurationCallback((builder, options) =>
            {
                // Just verify callback registration works
            });

        Assert.NotNull(syringe);
    }

    [Fact]
    public void UsingOptions_WithNullFactory_ThrowsArgumentNullException()
    {
        var syringe = new HostSyringe(new Syringe().UsingReflection());
        Assert.Throws<ArgumentNullException>(() => syringe.UsingOptions((Func<CreateHostOptions>)null!));
    }

    [Fact]
    public void UsingConfigurationCallback_WithNullCallback_ThrowsArgumentNullException()
    {
        var syringe = new HostSyringe(new Syringe().UsingReflection());
        Assert.Throws<ArgumentNullException>(() => syringe.UsingConfigurationCallback(null!));
    }

    [Fact]
    public void UsingHostFactory_WithNullFactory_ThrowsArgumentNullException()
    {
        var syringe = new HostSyringe(new Syringe().UsingReflection());
        Assert.Throws<ArgumentNullException>(() => syringe.UsingHostFactory(null!));
    }

    #region UsingOptions(ILogger) Tests

    [Fact]
    public void UsingOptions_WithLogger_WithNullSyringe_ThrowsArgumentNullException()
    {
        HostSyringe syringe = null!;
        var logger = LoggerFactory.Create(_ => { }).CreateLogger("Test");

        Assert.Throws<ArgumentNullException>(() =>
            syringe.UsingOptions(logger));
    }

    [Fact]
    public void UsingOptions_WithLogger_WithNullLogger_ThrowsArgumentNullException()
    {
        var syringe = new HostSyringe(new Syringe().UsingReflection());

        Assert.Throws<ArgumentNullException>(() =>
            syringe.UsingOptions((ILogger)null!));
    }

    [Fact]
    public void UsingOptions_WithLogger_ReturnsNewInstance()
    {
        var syringe = new HostSyringe(new Syringe().UsingReflection());
        var logger = LoggerFactory.Create(_ => { }).CreateLogger("Test");

        var result = syringe.UsingOptions(logger);

        Assert.NotNull(result);
        Assert.NotSame(syringe, result);
    }

    [Fact]
    public void UsingOptions_WithLogger_CanChainWithOtherMethods()
    {
        var syringe = new HostSyringe(new Syringe().UsingReflection());
        var logger = LoggerFactory.Create(_ => { }).CreateLogger("Test");

        var result = syringe
            .UsingOptions(logger)
            .UsingConfigurationCallback((builder, options) => { });

        Assert.NotNull(result);
    }

    #endregion
}
