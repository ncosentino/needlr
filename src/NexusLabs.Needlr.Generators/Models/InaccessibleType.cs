namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a type that would be injectable but is inaccessible (internal/private).
/// </summary>
internal readonly struct InaccessibleType
{
    public InaccessibleType(string typeName, string assemblyName)
    {
        TypeName = typeName;
        AssemblyName = assemblyName;
    }

    public string TypeName { get; }
    public string AssemblyName { get; }
}
