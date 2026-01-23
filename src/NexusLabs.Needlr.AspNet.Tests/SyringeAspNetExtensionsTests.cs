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
