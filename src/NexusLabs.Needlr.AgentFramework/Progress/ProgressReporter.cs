namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Default <see cref="IProgressReporter"/> that fans out events to all registered sinks.
/// </summary>
[DoNotAutoRegister]
internal sealed class ProgressReporter : IProgressReporter
{
    private readonly IReadOnlyList<IProgressSink> _sinks;
    private readonly IProgressSequence _sequence;
    private readonly string? _parentAgentId;

    internal ProgressReporter(
        string workflowId,
        IReadOnlyList<IProgressSink> sinks,
        IProgressSequence sequence,
        string? agentId = null,
        string? parentAgentId = null,
        int depth = 0)
    {
        WorkflowId = workflowId;
        _sinks = sinks;
        _sequence = sequence;
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
    public long NextSequence() => _sequence.Next();

    /// <inheritdoc />
    public void Report(IProgressEvent progressEvent)
    {
        if (_sinks.Count == 0) return;

        for (int i = 0; i < _sinks.Count; i++)
        {
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
            _sequence,
            agentId: agentId,
            parentAgentId: AgentId,
            depth: Depth + 1);
}
