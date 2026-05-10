namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// Thrown by <see cref="ITieredProviderSelector{TQuery, TResult}"/> when no enabled
/// providers were registered, so there is nothing to attempt.
/// </summary>
/// <remarks>
/// Inherits from <see cref="NoProvidersAvailableException"/> so callers can choose to
/// catch the base type for both "no providers registered" and "all providers failed"
/// conditions, or catch this type specifically.
/// </remarks>
public sealed class NoProvidersRegisteredException : NoProvidersAvailableException
{
    private const string DefaultMessage = "No enabled providers are registered.";

    /// <summary>
    /// Initializes a new instance with the default message.
    /// </summary>
    public NoProvidersRegisteredException()
        : base(DefaultMessage)
    {
    }

    /// <param name="message">A description of why no providers are registered.</param>
    public NoProvidersRegisteredException(string message)
        : base(message)
    {
    }
}
