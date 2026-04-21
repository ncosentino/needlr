namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Aggregated result of a DAG workflow run, extending <see cref="IPipelineRunResult"/>
/// with per-node diagnostics, edge metadata, and parallel branch grouping.
/// </summary>
/// <remarks>
/// <para>
/// Returned by DAG workflow execution. The base <see cref="IPipelineRunResult.Stages"/>
/// list is preserved for backward compatibility with consumers that expect a flat
/// stage list; <see cref="NodeResults"/> provides the richer graph-aware view.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var dagResult = (IDagRunResult)pipelineResult;
/// foreach (var (nodeId, nodeResult) in dagResult.NodeResults)
/// {
///     Console.WriteLine($"  {nodeId}: {nodeResult.Kind}, " +
///         $"{nodeResult.Duration.TotalMilliseconds}ms");
/// }
/// foreach (var (branchId, stages) in dagResult.BranchResults)
/// {
///     Console.WriteLine($"  Branch {branchId}: {stages.Count} stages");
/// }
/// </code>
/// </example>
public interface IDagRunResult : IPipelineRunResult
{
    /// <summary>
    /// Gets per-node diagnostics keyed by node ID, including edge connectivity,
    /// timing offsets, and the <see cref="NodeKind"/> discriminator.
    /// </summary>
    IReadOnlyDictionary<string, IDagNodeResult> NodeResults { get; }

    /// <summary>
    /// Gets parallel branch groupings, where each key is a branch identifier
    /// and the value is the ordered list of stage results within that branch.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<IAgentStageResult>> BranchResults { get; }
}
