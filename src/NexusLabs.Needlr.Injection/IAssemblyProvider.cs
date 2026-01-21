using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Defines a provider that supplies the list of candidate assemblies for dependency injection.
/// Implement this interface to customize which assemblies are scanned for types.
/// </summary>
[DoNotAutoRegister]
public interface IAssemblyProvider
{
    IReadOnlyList<Assembly> GetCandidateAssemblies();
}
