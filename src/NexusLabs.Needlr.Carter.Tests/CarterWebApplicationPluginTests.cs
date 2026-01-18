using Carter;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AspNet;

using Xunit;

namespace NexusLabs.Needlr.Carter.Tests;

public sealed class CarterWebApplicationPluginTests
{
    [Fact]
    public void Configure_ThrowsOnNullOptions()
    {
        // Arrange
        var plugin = new CarterWebApplicationPlugin();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => plugin.Configure(null!));
    }

    [Fact]
    public void Configure_MapsCarterEndpoints()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddCarter();
        var app = builder.Build();

        var plugin = new CarterWebApplicationPlugin();
        var options = new WebApplicationPluginOptions(app, []);

        // Act - should not throw
        plugin.Configure(options);

        // Assert - MapCarter was called without throwing
        // We can't easily verify the endpoints without a full integration test
    }
}
