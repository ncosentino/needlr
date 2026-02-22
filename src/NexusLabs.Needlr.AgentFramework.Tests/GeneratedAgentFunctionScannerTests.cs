using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.FunctionScanners;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class GeneratedAgentFunctionScannerTests
{
    [Fact]
    public void ScanForFunctionTypes_ReturnsProvidedTypes()
    {
        var types = new[] { typeof(string), typeof(int) };
        var scanner = new GeneratedAgentFunctionScanner(types);

        var result = scanner.ScanForFunctionTypes();

        Assert.Equal(types, result);
    }

    [Fact]
    public void ScanForFunctionTypes_ReturnsEmptyListWhenNoTypes()
    {
        var scanner = new GeneratedAgentFunctionScanner(Array.Empty<Type>());

        var result = scanner.ScanForFunctionTypes();

        Assert.Empty(result);
    }

    [Fact]
    public void Constructor_ThrowsWhenFunctionTypesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GeneratedAgentFunctionScanner(null!));
    }
}
