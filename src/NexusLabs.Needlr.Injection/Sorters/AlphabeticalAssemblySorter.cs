using System.Reflection;

namespace NexusLabs.Needlr.Injection.Sorters;

/// <summary>
/// Sorts assemblies alphabetically by their file location.
/// </summary>
public sealed class AlphabeticalAssemblySorter : IAssemblySorter
{
    /// <inheritdoc />
    public IEnumerable<Assembly> Sort(IReadOnlyList<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        return assemblies.OrderBy(x => x.Location);
    }
}
