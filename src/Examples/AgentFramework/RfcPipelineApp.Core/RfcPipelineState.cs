namespace RfcPipelineApp.Core;

/// <summary>
/// Typed mutable state shared across all stages in an RFC pipeline run.
/// Mirrors the pattern of BrandGhost's ArticlePipelineRunAccessor but
/// without AsyncLocal — the runner passes it via StageExecutionContext.
/// </summary>
public sealed class RfcPipelineState
{
    /// <summary>Gets the RFC metadata populated progressively during the run.</summary>
    public RfcMetadata Metadata { get; }

    /// <summary>Gets the list of structural issues found during validation.</summary>
    public List<string> StructureIssues { get; } = [];

    /// <summary>Gets the list of findings from the technical review stage.</summary>
    public List<string> ReviewFindings { get; } = [];

    /// <summary>Gets the list of fixes that were applied during the pipeline run.</summary>
    public List<string> AppliedFixes { get; } = [];

    /// <summary>Gets or sets whether the structure validation stage passed.</summary>
    public bool StructureValidationPassed { get; set; }

    /// <summary>Gets or sets whether the technical review stage passed.</summary>
    public bool TechnicalReviewPassed { get; set; }

    /// <summary>Gets or sets whether the cold reader stage passed.</summary>
    public bool ColdReaderPassed { get; set; }

    /// <summary>Gets or sets the number of cold reader critique-revise attempts.</summary>
    public int ColdReaderAttempts { get; set; }

    /// <summary>
    /// Initializes a new <see cref="RfcPipelineState"/>.
    /// </summary>
    /// <param name="metadata">The RFC metadata instance to populate during the run.</param>
    public RfcPipelineState(RfcMetadata metadata)
    {
        Metadata = metadata;
    }
}
