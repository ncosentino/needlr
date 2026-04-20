namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Snapshot of the resolved configuration used for an <see cref="IIterativeAgentLoop"/> run.
/// Echoed on <see cref="IterativeLoopResult.Configuration"/> so consumers can inspect
/// the loop's settings after execution without referencing the original
/// <see cref="IterativeLoopOptions"/>.
/// </summary>
/// <param name="ToolResultMode">How tool results were fed back to the model.</param>
/// <param name="MaxIterations">Maximum iterations allowed.</param>
/// <param name="MaxToolRoundsPerIteration">Maximum tool-calling rounds per iteration in <see cref="Iterative.ToolResultMode.MultiRound"/>.</param>
/// <param name="MaxTotalToolCalls">Cumulative tool call limit, or <see langword="null"/> if unlimited.</param>
/// <param name="BudgetPressureThreshold">Token budget pressure threshold, or <see langword="null"/> if disabled.</param>
/// <param name="LoopName">Human-readable name used in diagnostics and progress events.</param>
/// <param name="CheckCompletionAfterToolCalls">When the <see cref="IterativeLoopOptions.IsComplete"/> predicate was checked relative to tool calls.</param>
public sealed record IterativeLoopConfiguration(
    ToolResultMode ToolResultMode,
    int MaxIterations,
    int MaxToolRoundsPerIteration,
    int? MaxTotalToolCalls,
    double? BudgetPressureThreshold,
    string LoopName,
    ToolCompletionCheckMode CheckCompletionAfterToolCalls);
