namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// Diagnostic context passed to <see cref="ProviderFailurePolicy.OnHit"/> when a
/// matching failure policy is applied to a provider's thrown exception.
/// </summary>
/// <param name="ProviderName">
/// Name of the failing provider (from <see cref="ITieredProvider{TQuery, TResult}.Name"/>).
/// </param>
/// <param name="Exception">
/// The exception that was thrown by
/// <see cref="ITieredProvider{TQuery, TResult}.ExecuteAsync"/> and matched by the policy.
/// </param>
/// <param name="SkipUntil">
/// The absolute UTC timestamp until which the provider will be skipped on subsequent
/// calls, or <see langword="null"/> when the matched policy specified
/// <see cref="ProviderFailurePolicy.SkipDuration"/> as <see langword="null"/> (i.e. no
/// cross-call skip applied). When the policy specified
/// <see cref="ProviderFailurePolicy.IndefiniteSkip"/>, this resolves to
/// <see cref="DateTimeOffset.MaxValue"/>.
/// </param>
public sealed record ProviderFailureContext(
    string ProviderName,
    Exception Exception,
    DateTimeOffset? SkipUntil);
