namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Default <see cref="IProgressReporter"/> that fans out events to all registered sinks.
/// Thread-safe sequence numbering via <see cref="Interlocked.Increment(ref long)"/>.
/// </summary>
[DoNotAutoRegister]
internal sealed class ProgressReporter : IProgressReporter
{
    private readonly IReadOnlyList<IProgressSink> _sinks;
    private readonly string? _parentAgentId;

    internal ProgressReporter(
        string workflowId,
        IReadOnlyList<IProgressSink> sinks,
        string? agentId = null,
        string? parentAgentId = null,
        int depth = 0)
    {
        WorkflowId = workflowId;
        _sinks = sinks;
        AgentId = agentId;
        _parentAgentId = parentAgentId;
        Depth = depth;
    }

    /// <inheritdoc />
    public string WorkflowId { get; }

    /// <inheritdoc />
    public string? AgentId { get; }

    /// <inheritdoc />
    public int Depth { get; }

    /// <inheritdoc />
    public void Report(IProgressEvent progressEvent)
    {
        if (_sinks.Count == 0) return;

        for (int i = 0; i < _sinks.Count; i++)
        {
            // Fire-and-forget for ValueTask sinks that complete synchronously.
            // Async sinks should use ChannelProgressReporter wrapper.
            var task = _sinks[i].OnEventAsync(progressEvent, CancellationToken.None);
            if (!task.IsCompletedSuccessfully)
            {
                task.AsTask().ContinueWith(
                    static t => { /* swallow exceptions from sinks */ },
                    TaskContinuationOptions.OnlyOnFaulted);
            }
        }
    }

    /// <inheritdoc />
    public IProgressReporter CreateChild(string agentId) =>
        new ProgressReporter(
            WorkflowId,
            _sinks,
            agentId: agentId,
            parentAgentId: AgentId,
            depth: Depth + 1);

}
