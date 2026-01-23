using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Moq;

using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Extensions.Logging.Tests;

public sealed class PostBuildServiceCollectionPluginOptionsExtensionsTests
{
    [Fact]
    public void GetLogger_Generic_ReturnsTypedLogger()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<PostBuildServiceCollectionPluginOptionsExtensionsTests>>();
        var mockProvider = new Mock<IServiceProvider>();
        mockProvider
            .Setup(p => p.GetService(typeof(ILogger<PostBuildServiceCollectionPluginOptionsExtensionsTests>)))
            .Returns(mockLogger.Object);
        
        var configuration = new Mock<IConfiguration>().Object;
        var assemblies = Array.Empty<Assembly>() as IReadOnlyList<Assembly>;
        var mockPluginFactory = new Mock<IPluginFactory>();
        var options = new PostBuildServiceCollectionPluginOptions(
            mockProvider.Object, configuration, assemblies, mockPluginFactory.Object);

        // Act
        var logger = options.GetLogger<PostBuildServiceCollectionPluginOptionsExtensionsTests>();

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void GetLogger_Generic_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        PostBuildServiceCollectionPluginOptions options = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => options.GetLogger<PostBuildServiceCollectionPluginOptionsExtensionsTests>());
    }

    [Fact]
    public void GetLogger_NonGeneric_ReturnsLogger()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var mockProvider = new Mock<IServiceProvider>();
        mockProvider
            .Setup(p => p.GetService(typeof(ILogger)))
            .Returns(mockLogger.Object);
        
        var configuration = new Mock<IConfiguration>().Object;
        var assemblies = Array.Empty<Assembly>() as IReadOnlyList<Assembly>;
        var mockPluginFactory = new Mock<IPluginFactory>();
        var options = new PostBuildServiceCollectionPluginOptions(
            mockProvider.Object, configuration, assemblies, mockPluginFactory.Object);

        // Act
        var logger = options.GetLogger();

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void GetLogger_NonGeneric_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        PostBuildServiceCollectionPluginOptions options = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => options.GetLogger());
    }
}
