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

    /// <summary>
    /// Controls how Needlr's diagnostics middleware creates
    /// <see cref="System.Diagnostics.Activity"/> spans for chat completion calls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When MEAI's <c>UseOpenTelemetry()</c> or MAF's <c>WithOpenTelemetry()</c> is
    /// also active, both Needlr and the upstream middleware create spans for the same
    /// chat completion call. Set this to
    /// <see cref="ChatCompletionActivityMode.EnrichParent"/> to avoid duplicate spans —
    /// Needlr will add its tags (sequence number, char counts, agent name) to the
    /// existing parent <c>gen_ai.*</c> activity instead of creating a new one.
    /// </para>
    /// <para>
    /// Tool call activities (<c>agent.tool</c>) are not affected by this setting —
    /// they are always created because neither MEAI nor MAF produces per-tool-call spans.
    /// </para>
    /// </remarks>
    public ChatCompletionActivityMode ChatCompletionActivityMode { get; set; } =
        ChatCompletionActivityMode.Always;

    internal string ResolvedActivitySourceName => ActivitySourceName ?? MeterName;
}
