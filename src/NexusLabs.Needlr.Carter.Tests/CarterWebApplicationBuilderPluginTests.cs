using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

using Moq;

using NexusLabs.Needlr.AspNet;

using Xunit;

namespace NexusLabs.Needlr.Carter.Tests;

public sealed class CarterWebApplicationBuilderPluginTests
{
    [Fact]
    public void Configure_AddCarterServices()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var mockLogger = new Mock<ILogger>();
        var mockPluginFactory = new Mock<IPluginFactory>();
        var plugin = new CarterWebApplicationBuilderPlugin();
        var options = new WebApplicationBuilderPluginOptions(builder, [], mockLogger.Object, mockPluginFactory.Object);

        // Act
        plugin.Configure(options);

        // Assert - Carter services should be added without throwing
        // The fact that Configure doesn't throw indicates success
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
        var mockPluginFactory = new Mock<IPluginFactory>();
        mockLogger.Setup(l => l.IsEnabled(LogLevel.Information)).Returns(true);
        
        var plugin = new CarterWebApplicationBuilderPlugin();
        var options = new WebApplicationBuilderPluginOptions(builder, [], mockLogger.Object, mockPluginFactory.Object);

        // Act
        plugin.Configure(options);

        // Assert - verify logging calls
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Carter services")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
