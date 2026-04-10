namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Default <see cref="IProgressReporterFactory"/> that creates reporters with
/// DI-registered default sinks or caller-provided per-orchestration sinks.
/// </summary>
[DoNotAutoRegister]
internal sealed class ProgressReporterFactory : IProgressReporterFactory
{
    private readonly IReadOnlyList<IProgressSink> _defaultSinks;
    private readonly IProgressSequence _sequence;
    private readonly IProgressReporterErrorHandler _errorHandler;

    internal ProgressReporterFactory(
        IEnumerable<IProgressSink> defaultSinks,
        IProgressSequence sequence,
        IProgressReporterErrorHandler? errorHandler = null)
    {
        _defaultSinks = defaultSinks.ToArray();
        _sequence = sequence;
        _errorHandler = errorHandler ?? new NullProgressReporterErrorHandler();
    }

    /// <inheritdoc />
    public IProgressReporter Create(string workflowId)
    {
        if (_defaultSinks.Count == 0)
            return NullProgressReporter.Instance;

        return new ProgressReporter(workflowId, _defaultSinks, _sequence, _errorHandler);
    }

    /// <inheritdoc />
    public IProgressReporter Create(string workflowId, IEnumerable<IProgressSink> sinks)
    {
        var sinkList = sinks as IReadOnlyList<IProgressSink> ?? sinks.ToArray();
        if (sinkList.Count == 0)
            return NullProgressReporter.Instance;

        return new ProgressReporter(workflowId, sinkList, _sequence, _errorHandler);
    }
}
