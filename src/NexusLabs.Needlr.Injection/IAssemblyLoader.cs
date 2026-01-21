using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Defines a loader that loads assemblies for dependency injection scanning.
/// Implement this interface to customize how assemblies are discovered and loaded.
/// </summary>
[DoNotAutoRegister]
public interface IAssemblyLoader
{
    IReadOnlyList<Assembly> LoadAssemblies(bool continueOnAssemblyError);
}
