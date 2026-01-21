using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Defines a sorter that determines the order in which assemblies are processed.
/// Assembly order can affect service registration priority when multiple implementations exist.
/// </summary>
[DoNotAutoRegister]
public interface IAssemblySorter
{
    IEnumerable<Assembly> Sort(IReadOnlyList<Assembly> assemblies);
}
