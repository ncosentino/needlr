using System.Reflection;

namespace NexusLabs.Needlr.Injection.Sorters;

public sealed class AlphabeticalAssemblySorter : IAssemblySorter
{
    public IEnumerable<Assembly> Sort(IReadOnlyList<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        return assemblies.OrderBy(x => x.Location);
    }
}
