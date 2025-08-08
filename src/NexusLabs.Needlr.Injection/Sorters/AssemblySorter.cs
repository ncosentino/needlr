﻿using System.Reflection;

namespace NexusLabs.Needlr.Injection.Sorters;

public sealed class AssemblySorter : IAssemblySorter
{
    private readonly IAssemblySorter _wrappedSorter;
    private readonly Func<IReadOnlyList<Assembly>, IEnumerable<Assembly>> _sortCallback;

    public AssemblySorter(
        IAssemblySorter wrappedSorter,
        Func<IReadOnlyList<Assembly>, IEnumerable<Assembly>> sortCallback)
    {
        ArgumentNullException.ThrowIfNull(wrappedSorter);
        ArgumentNullException.ThrowIfNull(sortCallback);
        _wrappedSorter = wrappedSorter;
        _sortCallback = sortCallback;
    }

    public IEnumerable<Assembly> Sort(IReadOnlyList<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var sortedAssemblies = _wrappedSorter.Sort(assemblies);
        return _sortCallback.Invoke(sortedAssemblies.ToArray());
    }
}
