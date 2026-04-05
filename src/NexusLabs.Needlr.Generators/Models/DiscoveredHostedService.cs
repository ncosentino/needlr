namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a discovered hosted service (BackgroundService or IHostedService implementation).
/// </summary>
internal readonly struct DiscoveredHostedService
{
    public DiscoveredHostedService(
        string typeName,
        string assemblyName,
        GeneratorLifetime lifetime,
        TypeDiscoveryHelper.ConstructorParameterInfo[] constructorParameters,
        string? sourceFilePath = null)
    {
        TypeName = typeName;
        AssemblyName = assemblyName;
        Lifetime = lifetime;
        ConstructorParameters = constructorParameters;
        SourceFilePath = sourceFilePath;
    }

    public string TypeName { get; }
    public string AssemblyName { get; }
    public GeneratorLifetime Lifetime { get; }
    public TypeDiscoveryHelper.ConstructorParameterInfo[] ConstructorParameters { get; }
    public string? SourceFilePath { get; }
}
