using NexusLabs.Needlr.AgentFramework.Iterative;

namespace IterativeTripPlannerApp.Core;

/// <summary>
/// Delegate-based lifecycle hooks for the trip planner.
/// All hooks are optional — pass <see langword="null"/> to skip.
/// </summary>
public sealed class TripPlannerHooks
{
    /// <summary>
    /// Called at the start of each iteration. Parameters: iteration number, context.
    /// </summary>
    public Func<int, IterativeContext, Task>? OnIterationStart { get; init; }

    /// <summary>
    /// Called after each tool call completes. Parameters: iteration number, tool call result.
    /// </summary>
    public Func<int, ToolCallResult, Task>? OnToolCall { get; init; }

    /// <summary>
    /// Called at the end of each iteration. Parameter: iteration record.
    /// </summary>
    public Func<IterationRecord, Task>? OnIterationEnd { get; init; }
}
