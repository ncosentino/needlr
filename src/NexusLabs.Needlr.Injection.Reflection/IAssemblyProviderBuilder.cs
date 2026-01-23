namespace NexusLabs.Needlr.Injection.Reflection;

/// <summary>
/// Defines a builder for constructing <see cref="IAssemblyProvider"/> instances with custom loaders.
/// Use this to configure how assemblies are discovered for dependency injection.
/// For assembly ordering, use <c>SyringeExtensions.OrderAssemblies</c> instead.
/// </summary>
[DoNotAutoRegister]
public interface IAssemblyProviderBuilder
{
    IAssemblyProvider Build();
    AssemblyProviderBuilder UseLoader(IAssemblyLoader loader);
}