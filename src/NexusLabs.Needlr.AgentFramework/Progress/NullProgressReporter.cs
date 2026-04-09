namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// No-op <see cref="IProgressReporter"/> used when no sinks are registered.
/// All methods are zero-overhead.
/// </summary>
[DoNotAutoRegister]
internal sealed class NullProgressReporter : IProgressReporter
{
    /// <summary>Singleton instance.</summary>
    internal static readonly NullProgressReporter Instance = new();

    /// <inheritdoc />
    public string WorkflowId => string.Empty;

    /// <inheritdoc />
    public string? AgentId => null;

    /// <inheritdoc />
    public int Depth => 0;

    /// <inheritdoc />
    public long NextSequence() => 0;

    /// <inheritdoc />
    public void Report(IProgressEvent progressEvent) { }

    /// <inheritdoc />
    public IProgressReporter CreateChild(string agentId) => this;
}
