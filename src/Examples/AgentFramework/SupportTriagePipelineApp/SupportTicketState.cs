namespace SupportTriagePipelineApp;

/// <summary>
/// Shared pipeline state that flows between all phases and stages.
/// Each stage reads/writes properties so downstream stages can
/// react to upstream decisions (e.g., skipping escalation when
/// urgency is low).
/// </summary>
internal sealed class SupportTicketState
{
    public string CustomerMessage { get; set; } = string.Empty;

    public string DetectedIntent { get; set; } = string.Empty;

    public int UrgencyScore { get; set; }

    public int ArticlesFound { get; set; }

    public int DraftWordCount { get; set; }

    public bool FactCheckPassed { get; set; }

    public bool SupervisorApproved { get; set; }

    public bool HandoffReady { get; set; }
}
