namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes aggregate publication health across item scopes and final result sinks.
/// </summary>
public enum ExperimentPublicationStatus
{
    /// <summary>No item scope or result sink attempted publication.</summary>
    NotRequested,

    /// <summary>Every attempted publication succeeded.</summary>
    Succeeded,

    /// <summary>At least one optional publication failed and no required publication failed.</summary>
    PartiallyFailed,

    /// <summary>At least one required publication failed.</summary>
    Failed,
}
