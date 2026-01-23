using NexusLabs.Needlr.SemanticKernel.PluginScanners;

using Xunit;

namespace NexusLabs.Needlr.SemanticKernel.Tests;

public sealed class GeneratedSemanticKernelPluginScannerTests
{
    [Fact]
    public void Constructor_WithValidPluginTypes_CreatesInstance()
    {
        // Arrange
        var pluginTypes = new List<Type> { typeof(TestPlugin) };

        // Act
        var scanner = new GeneratedSemanticKernelPluginScanner(pluginTypes);

        // Assert
        Assert.NotNull(scanner);
    }

    [Fact]
    public void Constructor_WithNullPluginTypes_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<Type> pluginTypes = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GeneratedSemanticKernelPluginScanner(pluginTypes));
    }

    [Fact]
    public void ScanForPluginTypes_ReturnsProvidedTypes()
    {
        // Arrange
        var pluginTypes = new List<Type> { typeof(TestPlugin), typeof(AnotherPlugin) };
        var scanner = new GeneratedSemanticKernelPluginScanner(pluginTypes);

        // Act
        var result = scanner.ScanForPluginTypes();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(typeof(TestPlugin), result);
        Assert.Contains(typeof(AnotherPlugin), result);
    }

    [Fact]
    public void ScanForPluginTypes_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var pluginTypes = new List<Type>();
        var scanner = new GeneratedSemanticKernelPluginScanner(pluginTypes);

        // Act
        var result = scanner.ScanForPluginTypes();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ScanForPluginTypes_ReturnsSameInstance()
    {
        // Arrange
        var pluginTypes = new List<Type> { typeof(TestPlugin) };
        var scanner = new GeneratedSemanticKernelPluginScanner(pluginTypes);

        // Act
        var result1 = scanner.ScanForPluginTypes();
        var result2 = scanner.ScanForPluginTypes();

        // Assert
        Assert.Same(result1, result2);
    }

    // Test plugin classes
    private sealed class TestPlugin { }
    private sealed class AnotherPlugin { }
}
