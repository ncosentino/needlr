using System.Reflection;

namespace NexusLabs.Needlr.Injection.Sorters;

public sealed class LibTestEntryAssemblySorter : IAssemblySorter
{
    private readonly Predicate<Assembly> _isTestAssembly;
    private readonly Predicate<Assembly> _isClassLibrary;
    private readonly Predicate<Assembly> _isEntryPoint;

    public LibTestEntryAssemblySorter(
        Predicate<Assembly> isTestAssembly,
        Predicate<Assembly> isClassLibrary,
        Predicate<Assembly> isEntryPoint)
    {
        ArgumentNullException.ThrowIfNull(isTestAssembly);
        ArgumentNullException.ThrowIfNull(isClassLibrary);
        ArgumentNullException.ThrowIfNull(isEntryPoint);
        _isTestAssembly = isTestAssembly;
        _isClassLibrary = isClassLibrary;
        _isEntryPoint = isEntryPoint;
    }

    public IEnumerable<Assembly> Sort(IReadOnlyList<Assembly> assemblies)
    {
        var nonTestClassLibs = assemblies
            .Where(a =>
            {
                return !_isTestAssembly.Invoke(a)
                    && _isClassLibrary(a)
                    && !_isEntryPoint(a);
            })
            .ToList();
        var nonTestExecutables = assemblies
            .Where(a =>
            {
                return !_isTestAssembly.Invoke(a)
                    && _isEntryPoint(a);
            })
            .ToList();
        var testAssemblies = assemblies
            .Where(a =>
            {
                return _isTestAssembly.Invoke(a);
            })
            .ToList();

        var sortedAssemblies = nonTestClassLibs
            .Concat(nonTestExecutables)
            .Concat(testAssemblies)
            .ToArray();

        return sortedAssemblies;
    }
}
