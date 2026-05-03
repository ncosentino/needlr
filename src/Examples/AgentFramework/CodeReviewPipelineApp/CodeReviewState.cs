namespace CodeReviewPipelineApp;

/// <summary>
/// Typed pipeline state that accumulates findings across all phases
/// of the code review pipeline.
/// </summary>
internal sealed class CodeReviewState
{
    public int HunkCount { get; set; }

    public int FileCount { get; set; }

    public List<string> AntiPatterns { get; } = [];

    public List<string> SecurityFindings { get; } = [];

    public List<string> ReviewComments { get; } = [];

    public bool FindingsPrioritized { get; set; }

    public string PrComment { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;
}
