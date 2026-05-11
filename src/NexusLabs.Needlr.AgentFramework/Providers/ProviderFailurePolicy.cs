namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// Declarative failure-handling rule applied by
/// <see cref="TieredProviderSelector{TQuery, TResult}"/> when a provider throws an
/// exception during <see cref="ITieredProvider{TQuery, TResult}.ExecuteAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// Policies are evaluated in order against the thrown exception (first match wins). The
/// first policy whose <see cref="Match"/> predicate returns <see langword="true"/>
/// causes the selector to:
/// </para>
/// <list type="number">
///   <item>Add a per-provider attempt diagnostic to the chain and continue to the next provider.</item>
///   <item>Mark the provider as skipped for the duration in <see cref="SkipDuration"/> (if non-null), so subsequent calls bypass it without an attempt until the skip window elapses.</item>
///   <item>Invoke the <see cref="OnHit"/> callback (if non-null) with a <see cref="ProviderFailureContext"/> describing the failure.</item>
/// </list>
/// <para>
/// If no policy matches the thrown exception, the selector re-throws the exception
/// unchanged. The default policy in
/// <see cref="TieredProviderSelectorOptions.Default"/> matches
/// <see cref="ProviderUnavailableException"/> with no skip and no callback, preserving
/// the framework's historical fall-through behaviour.
/// </para>
/// <para>
/// <b>Cancellation is not subject to policy matching:</b> the selector skips policy
/// evaluation entirely when the active <see cref="CancellationToken"/> has been
/// cancelled, so cancelled calls always propagate
/// <see cref="OperationCanceledException"/> directly to the caller.
/// </para>
/// <para>
/// <b>Callback exceptions propagate.</b> If <see cref="OnHit"/> throws, the selector
/// still releases quota for the failed attempt (the release happens in a
/// <see langword="finally"/> block) and the callback's exception escapes
/// <see cref="ITieredProviderSelector{TQuery, TResult}.ExecuteAsync"/>. Subsequent
/// providers are not attempted for that call.
/// </para>
/// </remarks>
/// <param name="Match">
/// Predicate evaluated against each thrown exception to determine whether this policy
/// applies. The first matching policy in
/// <see cref="TieredProviderSelectorOptions.FailurePolicies"/> wins.
/// </param>
/// <param name="SkipDuration">
/// Optional duration the failing provider should be skipped before being retried on
/// subsequent calls.
/// <list type="bullet">
///   <item><see langword="null"/> — no cross-call skip; the provider is retried on the next call.</item>
///   <item>A finite <see cref="TimeSpan"/> — the provider is skipped until <c>now + SkipDuration</c>.</item>
///   <item><see cref="IndefiniteSkip"/> (<see cref="TimeSpan.MaxValue"/>) — the provider is skipped until process restart (resolves to <see cref="DateTimeOffset.MaxValue"/>; the selector clamps the addition to avoid overflow).</item>
/// </list>
/// </param>
/// <param name="OnHit">
/// Optional async callback invoked after the policy match is recorded but before
/// fall-through to the next provider. Receives a <see cref="ProviderFailureContext"/>
/// describing the failed provider, the exception, and the resulting skip-until
/// timestamp (if any).
/// </param>
/// <example>
/// <code>
/// // Treat ApiAuthException like ProviderUnavailableException, but skip for 5 minutes
/// // and emit a structured log.
/// var policy = new ProviderFailurePolicy(
///     Match: ex => ex is ApiAuthException,
///     SkipDuration: TimeSpan.FromMinutes(5),
///     OnHit: ctx =>
///     {
///         logger.LogWarning(ctx.Exception, "Provider {Provider} skipped until {Until}",
///             ctx.ProviderName, ctx.SkipUntil);
///         return ValueTask.CompletedTask;
///     });
///
/// var options = TieredProviderSelectorOptions.Default with
/// {
///     FailurePolicies = [.. TieredProviderSelectorOptions.Default.FailurePolicies, policy],
/// };
/// </code>
/// </example>
public sealed record ProviderFailurePolicy(
    Predicate<Exception> Match,
    TimeSpan? SkipDuration = null,
    Func<ProviderFailureContext, ValueTask>? OnHit = null)
{
    /// <summary>
    /// Sentinel value for <see cref="SkipDuration"/> meaning "skip indefinitely
    /// (until the host process restarts)." Resolves to
    /// <see cref="DateTimeOffset.MaxValue"/> in the selector's skip cache; the
    /// selector clamps the <c>now + SkipDuration</c> addition so passing this value
    /// will not overflow.
    /// </summary>
    public static TimeSpan IndefiniteSkip => TimeSpan.MaxValue;
}
