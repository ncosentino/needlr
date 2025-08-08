using System.Reflection;

namespace NexusLabs.Needlr.Injection;

[DoNotAutoRegister]
public interface IAssemblyLoader
{
    IReadOnlyList<Assembly> LoadAssemblies(bool continueOnAssemblyError);
}
