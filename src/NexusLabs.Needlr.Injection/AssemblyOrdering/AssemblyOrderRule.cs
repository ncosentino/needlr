using System.Linq.Expressions;

namespace NexusLabs.Needlr.Injection.AssemblyOrdering;

/// <summary>
/// Represents a single ordering rule that matches assemblies to a tier.
/// </summary>
public sealed class AssemblyOrderRule
{
    /// <summary>
    /// The expression that determines if an assembly matches this rule.
    /// </summary>
    public Expression<Func<AssemblyInfo, bool>> Expression { get; }

    /// <summary>
    /// The compiled predicate for runtime execution.
    /// </summary>
    public Func<AssemblyInfo, bool> CompiledPredicate { get; }

    /// <summary>
    /// The tier index (lower = earlier in sort order).
    /// </summary>
    public int Tier { get; }

    internal AssemblyOrderRule(Expression<Func<AssemblyInfo, bool>> expression, int tier)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        CompiledPredicate = expression.Compile();
        Tier = tier;
    }
}
