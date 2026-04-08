namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Default <see cref="IProgressReporterFactory"/> that creates reporters with
/// DI-registered default sinks or caller-provided per-orchestration sinks.
/// </summary>
[DoNotAutoRegister]
internal sealed class ProgressReporterFactory : IProgressReporterFactory
{
    private readonly IReadOnlyList<IProgressSink> _defaultSinks;

    internal ProgressReporterFactory(IEnumerable<IProgressSink> defaultSinks)
    {
        _defaultSinks = defaultSinks.ToArray();
    }

    /// <inheritdoc />
    public IProgressReporter Create(string workflowId)
    {
        if (_defaultSinks.Count == 0)
            return NullProgressReporter.Instance;

        return new ProgressReporter(workflowId, _defaultSinks);
    }

    /// <inheritdoc />
    public IProgressReporter Create(string workflowId, IEnumerable<IProgressSink> sinks)
    {
        var sinkList = sinks as IReadOnlyList<IProgressSink> ?? sinks.ToArray();
        if (sinkList.Count == 0)
            return NullProgressReporter.Instance;

        return new ProgressReporter(workflowId, sinkList);
    }
}
