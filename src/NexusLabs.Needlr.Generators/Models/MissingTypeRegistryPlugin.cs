namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a plugin type from a referenced assembly that's missing [GenerateTypeRegistry].
/// </summary>
internal readonly struct MissingTypeRegistryPlugin
{
    public MissingTypeRegistryPlugin(string typeName, string assemblyName)
    {
        TypeName = typeName;
        AssemblyName = assemblyName;
    }

    public string TypeName { get; }
    public string AssemblyName { get; }
}
