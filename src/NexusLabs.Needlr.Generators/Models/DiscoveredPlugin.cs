namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a discovered plugin type (implements INeedlrPlugin interfaces).
/// </summary>
internal readonly struct DiscoveredPlugin
{
    public DiscoveredPlugin(string typeName, string[] interfaceNames, string assemblyName, string[] attributeNames, string? sourceFilePath = null, int order = 0)
    {
        TypeName = typeName;
        InterfaceNames = interfaceNames;
        AssemblyName = assemblyName;
        AttributeNames = attributeNames;
        SourceFilePath = sourceFilePath;
        Order = order;
    }

    public string TypeName { get; }
    public string[] InterfaceNames { get; }
    public string AssemblyName { get; }
    public string[] AttributeNames { get; }
    public string? SourceFilePath { get; }
    public int Order { get; }
}
