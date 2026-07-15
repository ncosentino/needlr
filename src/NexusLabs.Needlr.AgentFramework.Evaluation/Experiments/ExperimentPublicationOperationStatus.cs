namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes whether one item scope or final sink completed its publication work.
/// </summary>
public enum ExperimentPublicationOperationStatus
{
    /// <summary>The scope completed its requested publication work.</summary>
    Succeeded,

    /// <summary>The scope attempted publication work and failed.</summary>
    Failed,

    /// <summary>The scope did not attempt publication work.</summary>
    NotAttempted,
}
