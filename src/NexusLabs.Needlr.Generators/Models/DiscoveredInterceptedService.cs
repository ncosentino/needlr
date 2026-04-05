namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about an intercepted service (from [Intercept&lt;T&gt;]).
/// </summary>
internal readonly struct DiscoveredInterceptedService
{
    public DiscoveredInterceptedService(
        string typeName,
        string[] interfaceNames,
        string assemblyName,
        GeneratorLifetime lifetime,
        InterceptorDiscoveryHelper.InterceptedMethodInfo[] methods,
        string[] allInterceptorTypeNames,
        string? sourceFilePath = null)
    {
        TypeName = typeName;
        InterfaceNames = interfaceNames;
        AssemblyName = assemblyName;
        Lifetime = lifetime;
        Methods = methods;
        AllInterceptorTypeNames = allInterceptorTypeNames;
        SourceFilePath = sourceFilePath;
    }

    public string TypeName { get; }
    public string[] InterfaceNames { get; }
    public string AssemblyName { get; }
    public GeneratorLifetime Lifetime { get; }
    public InterceptorDiscoveryHelper.InterceptedMethodInfo[] Methods { get; }
    public string[] AllInterceptorTypeNames { get; }
    public string? SourceFilePath { get; }
}
