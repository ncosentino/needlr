using Carter;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using NexusLabs.Needlr.AspNet;

using Xunit;

namespace NexusLabs.Needlr.Carter.Tests;

public sealed class CarterWebApplicationBuilderPluginIntegrationTests
{
    [Fact]
    public void Configure_AddsCarterServices()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var mockLogger = new Mock<ILogger>();
        var plugin = new CarterWebApplicationBuilderPlugin();
        var options = new WebApplicationBuilderPluginOptions(builder, [], mockLogger.Object);

        // Act
        plugin.Configure(options);

        // Assert - Carter services should be registered
        var app = builder.Build();
        var carterConfigurator = app.Services.GetService<CarterConfigurator>();
        Assert.NotNull(carterConfigurator);
    }

    [Fact]
    public void Configure_ThrowsOnNullOptions()
    {
        // Arrange
        var plugin = new CarterWebApplicationBuilderPlugin();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => plugin.Configure(null!));
    }

    [Fact]
    public void Configure_LogsInformation()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var mockLogger = new Mock<ILogger>();
        mockLogger.Setup(l => l.IsEnabled(LogLevel.Information)).Returns(true);

        var plugin = new CarterWebApplicationBuilderPlugin();
        var options = new WebApplicationBuilderPluginOptions(builder, [], mockLogger.Object);

        // Act
        plugin.Configure(options);

        // Assert - verify logging
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Carter")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Configure_CanBeCalledMultipleTimes()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var mockLogger = new Mock<ILogger>();
        var plugin = new CarterWebApplicationBuilderPlugin();
        var options = new WebApplicationBuilderPluginOptions(builder, [], mockLogger.Object);

        // Act - calling twice should not throw
        var exception = Record.Exception(() =>
        {
            plugin.Configure(options);
            plugin.Configure(options);
        });

        // Assert
        Assert.Null(exception);
    }
}

public sealed class CarterModuleIntegrationTests
{
    [Fact]
    public void MapCarter_RegistersModuleRoutes()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddCarter();

        var app = builder.Build();

        // Act - MapCarter should register routes from ICarterModule implementations
        var exception = Record.Exception(() => app.MapCarter());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void CarterPlugin_WorksWithPluginSystem()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var mockLogger = new Mock<ILogger>();

        // Configure via builder plugin
        var builderPlugin = new CarterWebApplicationBuilderPlugin();
        var builderOptions = new WebApplicationBuilderPluginOptions(builder, [], mockLogger.Object);
        builderPlugin.Configure(builderOptions);

        var app = builder.Build();

        // Configure via app plugin
        var appPlugin = new CarterWebApplicationPlugin();
        var appOptions = new WebApplicationPluginOptions(app, []);

        // Act
        var exception = Record.Exception(() => appPlugin.Configure(appOptions));

        // Assert
        Assert.Null(exception);
    }
}
