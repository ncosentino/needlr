namespace NexusLabs.Needlr.Injection.Reflection;

/// <summary>
/// Defines a builder for constructing <see cref="IAssemblyProvider"/> instances with custom loaders and sorters.
/// Use this to configure how assemblies are discovered and ordered for dependency injection.
/// </summary>
[DoNotAutoRegister]
public interface IAssemblyProviderBuilder
{
    IAssemblyProvider Build();
    AssemblyProviderBuilder UseLoader(IAssemblyLoader loader);
    AssemblyProviderBuilder UseSorter(IAssemblySorter sorter);
}