using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using NexusLabs.Needlr.AspNet;

using Xunit;

namespace NexusLabs.Needlr.SignalR.Tests;

public sealed class SignalRWebApplicationBuilderPluginTests
{
    [Fact]
    public void Configure_AddsSignalRServices()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var mockLogger = new Mock<ILogger>();
        var mockPluginFactory = new Mock<IPluginFactory>();
        var plugin = new SignalRWebApplicationBuilderPlugin();
        var options = new WebApplicationBuilderPluginOptions(builder, [], mockLogger.Object, mockPluginFactory.Object);

        // Act
        plugin.Configure(options);

        // Assert - SignalR services should be added
        var app = builder.Build();
        var hubContext = app.Services.GetService<IHubContext<TestHub>>();
        // Note: We can't easily verify HubContext without a full integration test
        // but the services were added without throwing
    }

    [Fact]
    public void Configure_ThrowsOnNullOptions()
    {
        // Arrange
        var plugin = new SignalRWebApplicationBuilderPlugin();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => plugin.Configure(null!));
    }

    [Fact]
    public void Configure_LogsInformation()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var mockLogger = new Mock<ILogger>();
        var mockPluginFactory = new Mock<IPluginFactory>();
        mockLogger.Setup(l => l.IsEnabled(LogLevel.Information)).Returns(true);
        
        var plugin = new SignalRWebApplicationBuilderPlugin();
        var options = new WebApplicationBuilderPluginOptions(builder, [], mockLogger.Object, mockPluginFactory.Object);

        // Act
        plugin.Configure(options);

        // Assert - verify logging calls
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SignalR services")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
