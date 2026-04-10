namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// No-op <see cref="IProgressReporter"/> used when no sinks are registered.
/// All methods are zero-overhead.
/// </summary>
[DoNotAutoRegister]
internal sealed class NullProgressReporter : IProgressReporter
{
    private static long _globalSequence;

    /// <summary>Singleton instance.</summary>
    internal static readonly NullProgressReporter Instance = new();

    /// <inheritdoc />
    public string WorkflowId => string.Empty;

    /// <inheritdoc />
    public string? AgentId => null;

    /// <inheritdoc />
    public int Depth => 0;

    /// <inheritdoc />
    /// <remarks>
    /// Returns real monotonically increasing values even from the null reporter
    /// so sequence ordering logic isn't silently broken. <see cref="Report"/> is
    /// still a no-op — sequences are generated but events are discarded.
    /// </remarks>
    public long NextSequence() => Interlocked.Increment(ref _globalSequence);

    /// <inheritdoc />
    public void Report(IProgressEvent progressEvent) { }

    /// <inheritdoc />
    public IProgressReporter CreateChild(string agentId) => this;
}
