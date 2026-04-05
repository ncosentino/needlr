namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a factory-generated type (from [GenerateFactory]).
/// </summary>
internal readonly struct DiscoveredFactory
{
    public DiscoveredFactory(
        string typeName,
        string[] interfaceNames,
        string assemblyName,
        int generationMode,
        FactoryDiscoveryHelper.FactoryConstructorInfo[] constructors,
        string? returnTypeName = null,
        string? sourceFilePath = null)
    {
        TypeName = typeName;
        InterfaceNames = interfaceNames;
        AssemblyName = assemblyName;
        GenerationMode = generationMode;
        Constructors = constructors;
        ReturnTypeOverride = returnTypeName;
        SourceFilePath = sourceFilePath;
    }

    public string TypeName { get; }
    public string[] InterfaceNames { get; }
    public string AssemblyName { get; }
    /// <summary>Mode flags: 1=Func, 2=Interface, 3=All</summary>
    public int GenerationMode { get; }
    public FactoryDiscoveryHelper.FactoryConstructorInfo[] Constructors { get; }
    /// <summary>
    /// If set, the factory Create() and Func return this type instead of the concrete type.
    /// Used when [GenerateFactory&lt;T&gt;] is applied.
    /// </summary>
    public string? ReturnTypeOverride { get; }
    public string? SourceFilePath { get; }

    public bool GenerateFunc => (GenerationMode & 1) != 0;
    public bool GenerateInterface => (GenerationMode & 2) != 0;

    /// <summary>Gets the type that factory Create() and Func should return.</summary>
    public string ReturnTypeName => ReturnTypeOverride ?? TypeName;

    /// <summary>Gets just the type name without namespace (e.g., "MyService" from "global::TestApp.MyService").</summary>
    public string SimpleTypeName
    {
        get
        {
            var parts = TypeName.Split('.');
            return parts[parts.Length - 1];
        }
    }
}
