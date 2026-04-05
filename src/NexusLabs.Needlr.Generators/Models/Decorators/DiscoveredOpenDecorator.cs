using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about an open-generic decorator (from [OpenDecoratorFor(typeof(IHandler&lt;&gt;))]).
/// </summary>
internal readonly struct DiscoveredOpenDecorator
{
    public DiscoveredOpenDecorator(
        INamedTypeSymbol decoratorType,
        INamedTypeSymbol openGenericInterface,
        int order,
        string assemblyName,
        string? sourceFilePath = null)
    {
        DecoratorType = decoratorType;
        OpenGenericInterface = openGenericInterface;
        Order = order;
        AssemblyName = assemblyName;
        SourceFilePath = sourceFilePath;
    }

    public INamedTypeSymbol DecoratorType { get; }
    public INamedTypeSymbol OpenGenericInterface { get; }
    public int Order { get; }
    public string AssemblyName { get; }
    public string? SourceFilePath { get; }
}
