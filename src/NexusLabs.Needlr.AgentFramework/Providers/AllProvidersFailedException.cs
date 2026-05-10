namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// Thrown by <see cref="ITieredProviderSelector{TQuery, TResult}"/> when at least one
/// provider was registered but every provider failed or was denied by the quota gate.
/// </summary>
/// <remarks>
/// Inherits from <see cref="NoProvidersAvailableException"/> so callers can choose to
/// catch the base type for both "no providers registered" and "all providers failed"
/// conditions, or catch this type specifically.
/// </remarks>
public sealed class AllProvidersFailedException : NoProvidersAvailableException
{
    /// <summary>
    /// Gets the per-provider attempt diagnostics in the order they were tried.
    /// Each entry describes one provider's outcome (e.g., quota denial or unavailability reason).
    /// </summary>
    public IReadOnlyList<string> Attempts { get; }

    /// <param name="attempts">
    /// Per-provider attempt diagnostics in the order providers were tried.
    /// </param>
    public AllProvidersFailedException(IReadOnlyList<string> attempts)
        : base(BuildMessage(attempts))
    {
        ArgumentNullException.ThrowIfNull(attempts);
        Attempts = attempts;
    }

    /// <param name="attempts">
    /// Per-provider attempt diagnostics in the order providers were tried.
    /// </param>
    /// <param name="innerException">The underlying exception that caused this failure, if any.</param>
    public AllProvidersFailedException(IReadOnlyList<string> attempts, Exception? innerException)
        : base(BuildMessage(attempts), innerException)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        Attempts = attempts;
    }

    private static string BuildMessage(IReadOnlyList<string> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        return $"All providers failed. Attempts: [{string.Join("; ", attempts)}]";
    }
}
