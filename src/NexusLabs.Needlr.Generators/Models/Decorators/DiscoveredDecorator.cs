namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a closed-generic decorator (from [DecoratorFor&lt;T&gt;]).
/// </summary>
internal readonly struct DiscoveredDecorator
{
    public DiscoveredDecorator(string decoratorTypeName, string serviceTypeName, int order, string assemblyName, string? sourceFilePath = null)
    {
        DecoratorTypeName = decoratorTypeName;
        ServiceTypeName = serviceTypeName;
        Order = order;
        AssemblyName = assemblyName;
        SourceFilePath = sourceFilePath;
    }

    public string DecoratorTypeName { get; }
    public string ServiceTypeName { get; }
    public int Order { get; }
    public string AssemblyName { get; }
    public string? SourceFilePath { get; }
}
