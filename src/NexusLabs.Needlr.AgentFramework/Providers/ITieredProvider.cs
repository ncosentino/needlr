namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// A provider in a priority-ordered fallback chain. Providers are tried in order of
/// ascending <see cref="Priority"/> until one succeeds or all fail.
/// </summary>
/// <typeparam name="TQuery">The query/request type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface ITieredProvider<in TQuery, TResult>
{
    /// <summary>Gets the provider's display name (used in diagnostics and logging).</summary>
    string Name { get; }

    /// <summary>
    /// Gets the priority for ordering. Lower values = higher priority (tried first).
    /// </summary>
    int Priority { get; }

    /// <summary>Gets whether this provider is enabled and should participate in the chain.</summary>
    bool IsEnabled { get; }

    /// <summary>Executes the query against this provider.</summary>
    /// <exception cref="ProviderUnavailableException">
    /// The provider is temporarily unavailable. The selector will try the next provider.
    /// </exception>
    Task<TResult> ExecuteAsync(TQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Thrown when a provider is temporarily unavailable. The tiered selector catches this
/// and falls through to the next provider in the chain.
/// </summary>
public class ProviderUnavailableException : Exception
{
    /// <summary>Gets the name of the provider that was unavailable.</summary>
    public string ProviderName { get; }

    /// <param name="providerName">The provider that was unavailable.</param>
    /// <param name="message">A description of why the provider is unavailable.</param>
    /// <param name="innerException">The original exception, if any.</param>
    public ProviderUnavailableException(string providerName, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderName = providerName;
    }
}
