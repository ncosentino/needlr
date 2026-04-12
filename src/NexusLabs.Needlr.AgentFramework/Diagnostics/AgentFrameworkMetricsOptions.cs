namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Configuration options for the agent framework's OpenTelemetry metrics and tracing.
/// </summary>
/// <remarks>
/// <para>
/// Consumers that have existing dashboards keyed to a specific meter name (e.g.,
/// <c>"BrandGhost.Agents"</c>) can set <see cref="MeterName"/> to match, avoiding a
/// dashboard migration when adopting Needlr's <see cref="IAgentMetrics"/>.
/// </para>
/// <para>
/// Configure via the syringe:
/// <code>
/// .UsingAgentFramework(af => af
///     .ConfigureMetrics(o => o.MeterName = "MyApp.Agents"))
/// </code>
/// </para>
/// </remarks>
[DoNotAutoRegister]
public sealed class AgentFrameworkMetricsOptions
{
    /// <summary>
    /// The name used for the <see cref="System.Diagnostics.Metrics.Meter"/> that emits
    /// counters and histograms. Defaults to <c>"NexusLabs.Needlr.AgentFramework"</c>.
    /// </summary>
    public string MeterName { get; set; } = "NexusLabs.Needlr.AgentFramework";

    /// <summary>
    /// The name used for the <see cref="System.Diagnostics.ActivitySource"/> that emits
    /// distributed tracing spans. Defaults to <see cref="MeterName"/> when
    /// <see langword="null"/>.
    /// </summary>
    public string? ActivitySourceName { get; set; }

    internal string ResolvedActivitySourceName => ActivitySourceName ?? MeterName;
}
