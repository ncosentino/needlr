namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// Executes a query against a priority-ordered chain of <see cref="ITieredProvider{TQuery, TResult}"/>
/// instances, falling through to the next provider on failure or quota exhaustion.
/// </summary>
/// <typeparam name="TQuery">The query/request type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface ITieredProviderSelector<in TQuery, TResult>
{
    /// <summary>
    /// Executes the query against providers in priority order until one succeeds.
    /// </summary>
    /// <exception cref="NoProvidersRegisteredException">
    /// No enabled providers were registered, so there was nothing to attempt.
    /// </exception>
    /// <exception cref="AllProvidersFailedException">
    /// At least one provider was registered, but every provider failed or was denied by the quota gate.
    /// </exception>
    /// <remarks>
    /// Both exceptions inherit from <see cref="NoProvidersAvailableException"/>, so callers can
    /// catch that base type to handle both conditions uniformly.
    /// </remarks>
    Task<TResult> ExecuteAsync(TQuery query, CancellationToken cancellationToken);
}
