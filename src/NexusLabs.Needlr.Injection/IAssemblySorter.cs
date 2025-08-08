using System.Reflection;

namespace NexusLabs.Needlr.Injection;

[DoNotAutoRegister]
public interface IAssemblySorter
{
    IEnumerable<Assembly> Sort(IReadOnlyList<Assembly> assemblies);
}
