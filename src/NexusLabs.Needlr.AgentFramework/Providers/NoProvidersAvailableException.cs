namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// Base exception type thrown by <see cref="ITieredProviderSelector{TQuery, TResult}"/>
/// when it cannot return a result because no provider is available to serve the request.
/// </summary>
/// <remarks>
/// <para>
/// Concrete subtypes describe the specific reason no provider was available:
/// </para>
/// <list type="bullet">
///   <item><see cref="NoProvidersRegisteredException"/> — no enabled providers were registered.</item>
///   <item><see cref="AllProvidersFailedException"/> — every registered provider failed or was gated out.</item>
/// </list>
/// <para>
/// Callers can catch this base type to handle both conditions uniformly, or catch a
/// concrete subtype for more granular handling.
/// </para>
/// </remarks>
public class NoProvidersAvailableException : Exception
{
    /// <param name="message">A description of why no provider was available.</param>
    public NoProvidersAvailableException(string message)
        : base(message)
    {
    }

    /// <param name="message">A description of why no provider was available.</param>
    /// <param name="innerException">The underlying exception that caused this failure, if any.</param>
    public NoProvidersAvailableException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
