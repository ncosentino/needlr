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
    /// <exception cref="InvalidOperationException">All providers failed or none are enabled.</exception>
    Task<TResult> ExecuteAsync(TQuery query, CancellationToken cancellationToken);
}
