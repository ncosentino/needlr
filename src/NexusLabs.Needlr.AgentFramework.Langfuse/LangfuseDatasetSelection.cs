using System.Globalization;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Selects the latest state or an explicit timestamped version of a hosted Langfuse dataset.
/// </summary>
public sealed record LangfuseDatasetSelection
{
    /// <summary>Gets the project-unique hosted dataset name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the optional dataset version timestamp. A <see langword="null"/> value selects the latest
    /// state at query time.
    /// </summary>
    public DateTimeOffset? Version { get; init; }

    internal string? GetVersionText() =>
        Version?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    internal void Validate() =>
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
}
