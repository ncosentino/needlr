using System.Reflection;

namespace NexusLabs.Needlr.Injection.Reflection.Sorters;

/// <summary>
/// Assembly sorter that provides a default pass-through sorting behavior.
/// </summary>
public sealed class ReflectionAssemblySorter : IAssemblySorter
{
    public IEnumerable<Assembly> Sort(IReadOnlyList<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        return assemblies;
    }
}
