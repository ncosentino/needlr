using System.Linq.Expressions;
using System.Reflection;

namespace NexusLabs.Needlr.Injection.AssemblyOrdering;

/// <summary>
/// Fluent builder for specifying assembly ordering rules.
/// Assemblies are sorted into tiers based on the first matching rule.
/// Unmatched assemblies are placed last.
/// </summary>
public sealed class AssemblyOrderBuilder
{
    private readonly List<AssemblyOrderRule> _rules = new();
    private int _currentTier = 0;

    /// <summary>
    /// Adds the first ordering rule. Assemblies matching this expression go first.
    /// </summary>
    /// <param name="predicate">Expression that determines if an assembly matches this tier.</param>
    /// <returns>The builder for chaining.</returns>
    public AssemblyOrderBuilder By(Expression<Func<AssemblyInfo, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _rules.Add(new AssemblyOrderRule(predicate, _currentTier++));
        return this;
    }

    /// <summary>
    /// Adds a subsequent ordering rule. Assemblies matching this expression 
    /// (and not matching any previous rules) go in the next tier.
    /// </summary>
    /// <param name="predicate">Expression that determines if an assembly matches this tier.</param>
    /// <returns>The builder for chaining.</returns>
    public AssemblyOrderBuilder ThenBy(Expression<Func<AssemblyInfo, bool>> predicate)
    {
        if (_rules.Count == 0)
        {
            throw new InvalidOperationException("ThenBy() must be called after By().");
        }

        ArgumentNullException.ThrowIfNull(predicate);
        _rules.Add(new AssemblyOrderRule(predicate, _currentTier++));
        return this;
    }

    /// <summary>
    /// Gets the ordering rules that have been configured.
    /// </summary>
    public IReadOnlyList<AssemblyOrderRule> Rules => _rules;

    /// <summary>
    /// Sorts assemblies according to the configured rules.
    /// Each assembly is placed in the first tier it matches.
    /// Unmatched assemblies are placed last.
    /// </summary>
    /// <param name="assemblies">The assemblies to sort.</param>
    /// <returns>The sorted assemblies.</returns>
    public IReadOnlyList<Assembly> Sort(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var assemblyList = assemblies.ToList();
        if (assemblyList.Count == 0 || _rules.Count == 0)
        {
            return assemblyList;
        }

        var unmatchedTier = _currentTier; // Unmatched assemblies go last

        var tieredAssemblies = assemblyList
            .Select(assembly =>
            {
                var info = AssemblyInfo.FromAssembly(assembly);
                var tier = GetTier(info, unmatchedTier);
                return (Assembly: assembly, Tier: tier, Name: info.Name);
            })
            .OrderBy(x => x.Tier)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase) // Alphabetical within tier
            .Select(x => x.Assembly)
            .ToList();

        return tieredAssemblies;
    }

    /// <summary>
    /// Sorts assembly names according to the configured rules.
    /// Used for source-gen scenarios where only names are available.
    /// </summary>
    /// <param name="assemblyNames">The assembly names to sort.</param>
    /// <returns>The sorted assembly names.</returns>
    public IReadOnlyList<string> SortNames(IEnumerable<string> assemblyNames)
    {
        ArgumentNullException.ThrowIfNull(assemblyNames);

        var nameList = assemblyNames.ToList();
        if (nameList.Count == 0 || _rules.Count == 0)
        {
            return nameList;
        }

        var unmatchedTier = _currentTier;

        var tieredNames = nameList
            .Select(name =>
            {
                var info = AssemblyInfo.FromStrings(name);
                var tier = GetTier(info, unmatchedTier);
                return (Name: name, Tier: tier);
            })
            .OrderBy(x => x.Tier)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Name)
            .ToList();

        return tieredNames;
    }

    private int GetTier(AssemblyInfo info, int unmatchedTier)
    {
        foreach (var rule in _rules)
        {
            if (rule.CompiledPredicate(info))
            {
                return rule.Tier;
            }
        }
        return unmatchedTier;
    }
}
