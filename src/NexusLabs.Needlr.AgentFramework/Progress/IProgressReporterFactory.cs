namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Creates <see cref="IProgressReporter"/> instances scoped to a specific orchestration.
/// Each orchestrator can have its own set of sinks.
/// </summary>
public interface IProgressReporterFactory
{
    /// <summary>
    /// Creates a reporter using the default sinks registered in DI.
    /// </summary>
    /// <param name="workflowId">Correlation ID for the orchestration run.</param>
    IProgressReporter Create(string workflowId);

    /// <summary>
    /// Creates a reporter with specific sinks for this orchestration.
    /// </summary>
    /// <param name="workflowId">Correlation ID for the orchestration run.</param>
    /// <param name="sinks">Sinks to receive events for this orchestration only.</param>
    IProgressReporter Create(string workflowId, IEnumerable<IProgressSink> sinks);
}
