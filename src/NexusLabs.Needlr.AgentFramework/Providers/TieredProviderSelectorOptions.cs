namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// Configuration for <see cref="TieredProviderSelector{TQuery, TResult}"/> controlling
/// how exceptions thrown by providers are handled.
/// </summary>
/// <remarks>
/// <para>
/// The selector evaluates <see cref="FailurePolicies"/> in order against each thrown
/// exception (first match wins). When a policy matches, the selector falls through to
/// the next provider, optionally caches a per-provider skip-until timestamp so future
/// calls bypass the failing provider for a configurable duration, and optionally
/// invokes the policy's <see cref="ProviderFailurePolicy.OnHit"/> callback. When no
/// policy matches, the exception is re-thrown unchanged.
/// </para>
/// <para>
/// Use <see cref="Default"/> to preserve the framework's historical behaviour
/// (<see cref="ProviderUnavailableException"/> falls through, no skip, no callback) and
/// extend it with <c>with</c>-clones to add custom policies:
/// </para>
/// <code>
/// var options = TieredProviderSelectorOptions.Default with
/// {
///     FailurePolicies =
///     [
///         .. TieredProviderSelectorOptions.Default.FailurePolicies,
///         new ProviderFailurePolicy(
///             Match: ex => ex is ApiAuthException,
///             SkipDuration: TimeSpan.FromMinutes(5)),
///     ],
/// };
/// </code>
/// </remarks>
[DoNotAutoRegister]
public sealed record TieredProviderSelectorOptions
{
    /// <summary>
    /// Ordered list of failure policies. The first policy whose
    /// <see cref="ProviderFailurePolicy.Match"/> predicate returns <see langword="true"/>
    /// for a thrown exception is applied; subsequent policies are not evaluated.
    /// Defaults to an empty list (every exception propagates raw).
    /// </summary>
    public IReadOnlyList<ProviderFailurePolicy> FailurePolicies { get; init; } = [];

    /// <summary>
    /// Default options preserving the framework's historical fall-through behaviour:
    /// <see cref="ProviderUnavailableException"/> matches with no skip and no callback,
    /// so the selector falls through to the next provider on every PUE without any
    /// cross-call memory of the failure. Other exception types propagate raw.
    /// </summary>
    /// <remarks>
    /// Use this as the starting point for custom configurations:
    /// <c>TieredProviderSelectorOptions.Default with { FailurePolicies = [...] }</c>.
    /// </remarks>
    public static TieredProviderSelectorOptions Default { get; } = new()
    {
        FailurePolicies =
        [
            new ProviderFailurePolicy(
                Match: static ex => ex is ProviderUnavailableException,
                SkipDuration: null,
                OnHit: null),
        ],
    };
}
