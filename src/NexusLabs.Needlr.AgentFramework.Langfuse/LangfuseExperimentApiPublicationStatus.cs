namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Summarizes direct Langfuse REST publication observed by one experiment run instance.
/// </summary>
public enum LangfuseExperimentApiPublicationStatus
{
    /// <summary>No item-link or run-score operation has completed.</summary>
    NotAttempted = 0,

    /// <summary>At least one item-link or run-score operation is still running.</summary>
    InProgress = 1,

    /// <summary>Every requested operation reached a successful or intentionally skipped outcome.</summary>
    Complete = 2,

    /// <summary>Some operations completed, while others failed or could not be attempted.</summary>
    Partial = 3,

    /// <summary>No requested publication operation completed successfully.</summary>
    Failed = 4,

    /// <summary>Langfuse was disabled, so all operations were coherent no-ops.</summary>
    Disabled = 5,
}
