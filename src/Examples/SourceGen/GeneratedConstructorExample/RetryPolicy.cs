using NexusLabs.Needlr.Generators;

namespace GeneratedConstructorExample;

/// <summary>
/// DEMO 7 -- an explicit custom-guard method selector:
/// <c>[ConstructorGuard(typeof(NumberGuards), nameof(NumberGuards.ValidatePositive))]</c>.
/// The generator resolves <see cref="NumberGuards.ValidatePositive"/> at
/// compile time and emits a direct call -- no reflection, no conventional
/// <c>Validate</c> method name required.
/// </summary>
public partial class RetryPolicy
{
    [ConstructorGuard(typeof(NumberGuards), nameof(NumberGuards.ValidatePositive))]
    private readonly int _retryCount;

    /// <summary>The retry count supplied to the generated constructor.</summary>
    public int RetryCount => _retryCount;
}
