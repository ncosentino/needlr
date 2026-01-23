using Microsoft.AspNetCore.Builder;

using Moq;

using Xunit;

namespace NexusLabs.Needlr.AspNet.Tests;

/// <summary>
/// Tests for IWebApplicationFactoryExtensions.
/// </summary>
public sealed class IWebApplicationFactoryExtensionsTests
{
    [Fact]
    public void Create_WithOptionsOnly_WithNullFactory_ThrowsArgumentNullException()
    {
        IWebApplicationFactory factory = null!;
        var options = CreateWebApplicationOptions.Default;

        Assert.Throws<ArgumentNullException>(() => factory.Create(options));
    }

    [Fact]
    public void Create_WithOptionsOnly_WithNullOptions_ThrowsArgumentNullException()
    {
        var mockFactory = new Mock<IWebApplicationFactory>();
        CreateWebApplicationOptions options = null!;

        Assert.Throws<ArgumentNullException>(() => mockFactory.Object.Create(options));
    }

    [Fact]
    public void Create_WithCallback_WithNullFactory_ThrowsArgumentNullException()
    {
        IWebApplicationFactory factory = null!;
        var options = CreateWebApplicationOptions.Default;

        Assert.Throws<ArgumentNullException>(() =>
            factory.Create(options, (builder, opts) => { }));
    }

    [Fact]
    public void Create_WithCallback_WithNullOptions_ThrowsArgumentNullException()
    {
        var mockFactory = new Mock<IWebApplicationFactory>();
        CreateWebApplicationOptions options = null!;

        Assert.Throws<ArgumentNullException>(() =>
            mockFactory.Object.Create(options, (builder, opts) => { }));
    }

    [Fact]
    public void Create_WithNullCallback_DoesNotThrow()
    {
        // Null callback is allowed - it just means no additional configuration
        var mockFactory = new Mock<IWebApplicationFactory>();
        var options = CreateWebApplicationOptions.Default;

        // Setup mock to return a WebApplication when Create is called
        mockFactory
            .Setup(f => f.Create(
                It.IsAny<CreateWebApplicationOptions>(),
                It.IsAny<Func<WebApplicationBuilder>>()))
            .Returns(() => WebApplication.CreateBuilder().Build());

        // Should not throw - null! to suppress warning since null is intentional
        Action<WebApplicationBuilder, CreateWebApplicationOptions>? callback = null;
        var result = mockFactory.Object.Create(options, callback);

        Assert.NotNull(result);
    }
}
