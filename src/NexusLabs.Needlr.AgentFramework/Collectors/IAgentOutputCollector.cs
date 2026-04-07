namespace NexusLabs.Needlr.AgentFramework.Collectors;

/// <summary>
/// Accumulates typed records produced by agent tool calls during a pipeline run.
/// Provides a structured alternative to parsing free-form agent text output.
/// </summary>
/// <typeparam name="T">The record type to collect.</typeparam>
/// <remarks>
/// <para>
/// Tools call <see cref="Add"/> to emit structured output. After the agent run completes,
/// the orchestrator reads <see cref="Items"/> to process the collected records.
/// </para>
/// <para>
/// Example: a review agent calls a <c>RecordIssue</c> tool that adds <c>ReviewIssue</c>
/// records to the collector. The pipeline runner reads the issues after the review stage.
/// </para>
/// </remarks>
public interface IAgentOutputCollector<T>
{
    /// <summary>Adds a record to the collection. Thread-safe.</summary>
    void Add(T item);

    /// <summary>Gets all collected records in insertion order.</summary>
    IReadOnlyList<T> Items { get; }

    /// <summary>Gets the number of collected records.</summary>
    int Count { get; }
}
