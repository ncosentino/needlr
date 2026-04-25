namespace RfcPipelineApp.Core;

/// <summary>
/// Mutable metadata for the RFC document, populated progressively
/// as the pipeline executes drafting and review stages.
/// </summary>
public sealed class RfcMetadata
{
    /// <summary>Gets or sets the RFC title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets a one-paragraph executive summary.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the document status.
    /// Transitions: "" → "Draft" → "Review" → "Approved".
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets the list of contributing authors.</summary>
    public List<string> Authors { get; set; } = [];
}
