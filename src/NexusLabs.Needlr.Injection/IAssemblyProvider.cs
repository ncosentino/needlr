using System.Reflection;

namespace NexusLabs.Needlr.Injection;

[DoNotAutoRegister]
public interface IAssemblyProvider
{
    IReadOnlyList<Assembly> GetCandidateAssemblies();
}
