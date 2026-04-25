namespace RfcPipelineApp.Core;

/// <summary>
/// Describes a feature request to be turned into an RFC document.
/// </summary>
/// <param name="FeatureTitle">Short title for the proposed feature.</param>
/// <param name="Description">Detailed description of the feature and its motivation.</param>
/// <param name="Constraints">
/// Hard constraints that must be respected (e.g., backward compatibility,
/// performance SLAs, security requirements).
/// </param>
/// <param name="ExistingContext">
/// References to existing systems, documents, or prior art that inform the design.
/// </param>
/// <param name="TargetAudience">
/// The intended audience for the RFC document. Defaults to "engineering team".
/// </param>
public sealed record RfcAssignment(
    string FeatureTitle,
    string Description,
    string[] Constraints,
    string[] ExistingContext,
    string TargetAudience = "engineering team")
{
    /// <summary>Gets the workspace path for the RFC draft markdown.</summary>
    public string DraftPath => "rfc-draft.md";

    /// <summary>Gets the workspace path for the research brief.</summary>
    public string ResearchPath => "research-brief.md";

    /// <summary>Gets the workspace path for the document outline.</summary>
    public string OutlinePath => "outline.md";
}
