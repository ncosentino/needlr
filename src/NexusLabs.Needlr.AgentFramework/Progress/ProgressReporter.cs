namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Default <see cref="IProgressReporter"/> that fans out events to all registered sinks.
/// Sink exceptions are surfaced via an <see cref="IProgressReporterErrorHandler"/> instead
/// of being silently swallowed.
/// </summary>
[DoNotAutoRegister]
internal sealed class ProgressReporter : IProgressReporter
{
    private readonly IReadOnlyList<IProgressSink> _sinks;
    private readonly IProgressSequence _sequence;
    private readonly IProgressReporterErrorHandler _errorHandler;
    private readonly string? _parentAgentId;

    internal ProgressReporter(
        string workflowId,
        IReadOnlyList<IProgressSink> sinks,
        IProgressSequence sequence,
        IProgressReporterErrorHandler? errorHandler = null,
        string? agentId = null,
        string? parentAgentId = null,
        int depth = 0)
    {
        WorkflowId = workflowId;
        _sinks = sinks;
        _sequence = sequence;
        _errorHandler = errorHandler ?? new NullProgressReporterErrorHandler();
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
            var sink = _sinks[i];
            try
            {
                var task = sink.OnEventAsync(progressEvent, CancellationToken.None);
                if (!task.IsCompletedSuccessfully)
                {
                    var handler = _errorHandler;
                    var capturedSink = sink;
                    var capturedEvent = progressEvent;
                    task.AsTask().ContinueWith(
                        t =>
                        {
                            var ex = t.Exception?.GetBaseException();
                            if (ex is not null)
                                handler.OnSinkException(capturedSink, capturedEvent, ex);
                        },
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                }
            }
            catch (Exception ex)
            {
                _errorHandler.OnSinkException(sink, progressEvent, ex);
            }
        }
    }

    /// <inheritdoc />
    public IProgressReporter CreateChild(string agentId) =>
        new ProgressReporter(
            WorkflowId,
            _sinks,
            _sequence,
            _errorHandler,
            agentId: agentId,
            parentAgentId: AgentId,
            depth: Depth + 1);
}
