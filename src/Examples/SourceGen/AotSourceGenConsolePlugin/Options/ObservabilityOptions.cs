// Examples of nested options types - LoggingOptions and MetricsOptions are bound via parent
using NexusLabs.Needlr.Generators;

namespace AotSourceGenConsolePlugin.Options;

/// <summary>
/// Root options type - gets registered with Configure&lt;ObservabilityOptions&gt;().
/// Nested options (LoggingOptions, MetricsOptions) are bound automatically.
/// </summary>
[Options]
public class ObservabilityOptions
{
    public LoggingOptions Logging { get; set; } = new();
    public MetricsOptions Metrics { get; set; } = new();
}

/// <summary>
/// Nested options type - NOT registered separately.
/// Bound as part of ObservabilityOptions via configuration hierarchy.
/// </summary>
[Options]
public class LoggingOptions
{
    public string Level { get; set; } = "Information";
    public bool IncludeScopes { get; set; }
}

/// <summary>
/// Nested options type - NOT registered separately.
/// Bound as part of ObservabilityOptions via configuration hierarchy.
/// </summary>
[Options]
public class MetricsOptions
{
    public bool Enabled { get; set; } = true;
    public int FlushIntervalSeconds { get; set; } = 30;
}
