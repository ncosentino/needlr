namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// The outcome of a pipeline stage execution.
/// </summary>
public enum StageOutcome
{
    /// <summary>Stage executed successfully.</summary>
    Succeeded,

    /// <summary>Stage was skipped via a ShouldSkip predicate.</summary>
    Skipped,

    /// <summary>Stage executed but failed.</summary>
    Failed,
}
