using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace IterativeTripPlannerApp.Core;

/// <summary>
/// The result of a trip planner run, including the loop result, diagnostics,
/// and the final workspace state.
/// </summary>
public sealed record TripPlannerRunResult(
    IterativeLoopResult LoopResult,
    IAgentRunDiagnostics? Diagnostics,
    IWorkspace Workspace);
