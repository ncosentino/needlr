using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Configures the Langfuse dataset run created implicitly by experiment item links.
/// </summary>
public sealed record LangfuseExperimentRunOptions
{
    /// <summary>
    /// Gets or sets the optional run description submitted with every item link.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets optional structured metadata submitted with every item link.
    /// </summary>
    /// <remarks>
    /// The value is serialized and frozen when the experiment run is created. Later mutations to
    /// the original object do not change the submitted metadata.
    /// </remarks>
    public object? Metadata { get; init; }

    /// <summary>
    /// Gets or sets the optional hosted dataset version timestamp submitted with every item link.
    /// A <see langword="null"/> value links against the latest dataset state.
    /// </summary>
    public DateTimeOffset? DatasetVersion { get; init; }

    internal string? NormalizeDescription() =>
        string.IsNullOrWhiteSpace(Description) ? null : Description;

    internal JsonElement? FreezeMetadata() =>
        Metadata is null ? null : LangfuseApiClient.SerializeToElement(Metadata);

    internal DateTimeOffset? NormalizeDatasetVersion() =>
        DatasetVersion?.ToUniversalTime();
}
