using NexusLabs.Needlr.Injection;

using Xunit;

namespace NexusLabs.Needlr.AspNet.Tests;

/// <summary>
/// Tests for SyringeAspNetExtensions.
/// </summary>
public sealed class SyringeAspNetExtensionsTests
{
    [Fact]
    public void ForWebApplication_WithNullSyringe_ThrowsArgumentNullException()
    {
        Syringe syringe = null!;

        Assert.Throws<ArgumentNullException>(() => syringe.ForWebApplication());
    }

    [Fact]
    public void ForWebApplication_WithValidSyringe_ReturnsWebApplicationSyringe()
    {
        var syringe = new Syringe();

        var result = syringe.ForWebApplication();

        Assert.NotNull(result);
        Assert.IsType<WebApplicationSyringe>(result);
    }

    [Fact]
    public void BuildWebApplication_WithNullSyringe_ThrowsArgumentNullException()
    {
        Syringe syringe = null!;

        Assert.Throws<ArgumentNullException>(() => syringe.BuildWebApplication());
    }
}

/// <summary>
/// Tests for WebApplicationSyringeExtensions.
/// </summary>
public sealed class WebApplicationSyringeExtensionsTests
{
    [Fact]
    public void UsingOptions_WithNullSyringe_ThrowsArgumentNullException()
    {
        WebApplicationSyringe syringe = null!;

        Assert.Throws<ArgumentNullException>(() =>
            syringe.UsingOptions(() => CreateWebApplicationOptions.Default));
    }

    [Fact]
    public void UsingOptions_WithNullFactory_ThrowsArgumentNullException()
    {
        var syringe = new WebApplicationSyringe();

        Assert.Throws<ArgumentNullException>(() =>
            syringe.UsingOptions(null!));
    }

    [Fact]
    public void UsingOptions_WithValidFactory_ReturnsConfiguredSyringe()
    {
        var syringe = new WebApplicationSyringe();
        var options = CreateWebApplicationOptions.Default.UsingApplicationName("TestApp");

        var result = syringe.UsingOptions(() => options);

        Assert.NotNull(result);
        Assert.NotSame(syringe, result);
    }

    [Fact]
    public void UsingWebApplicationFactory_WithNullSyringe_ThrowsArgumentNullException()
    {
        WebApplicationSyringe syringe = null!;

        Assert.Throws<ArgumentNullException>(() =>
            syringe.UsingWebApplicationFactory((builder, populator) =>
                new WebApplicationFactory(builder, populator, null!)));
    }

    [Fact]
    public void UsingWebApplicationFactory_WithNullFactory_ThrowsArgumentNullException()
    {
        var syringe = new WebApplicationSyringe();

        Assert.Throws<ArgumentNullException>(() =>
            syringe.UsingWebApplicationFactory(null!));
    }

    [Fact]
    public void UsingConfigurationCallback_WithNullSyringe_ThrowsArgumentNullException()
    {
        WebApplicationSyringe syringe = null!;

        Assert.Throws<ArgumentNullException>(() =>
            syringe.UsingConfigurationCallback((builder, options) => { }));
    }

    [Fact]
    public void UsingConfigurationCallback_WithNullCallback_ThrowsArgumentNullException()
    {
        var syringe = new WebApplicationSyringe();

        Assert.Throws<ArgumentNullException>(() =>
            syringe.UsingConfigurationCallback(null!));
    }

    [Fact]
    public void UsingConfigurationCallback_WithValidCallback_ReturnsConfiguredSyringe()
    {
        var syringe = new WebApplicationSyringe();
        var callbackInvoked = false;

        var result = syringe.UsingConfigurationCallback((builder, options) =>
        {
            callbackInvoked = true;
        });

        Assert.NotNull(result);
        Assert.NotSame(syringe, result);
        // Note: callback is not invoked until BuildWebApplication is called
        Assert.False(callbackInvoked);
    }
}
