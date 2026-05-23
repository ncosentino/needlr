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
    /// The name used for the <see cref="System.Diagnostics.Metrics.Meter"/> that owns
    /// the <c>gen_ai.client.token.usage</c> histogram on which Needlr emits
    /// <c>cache_read</c> and <c>reasoning</c> measurements via
    /// <see cref="IGenAiTokenMetrics"/>. Defaults to <c>"Experimental.Microsoft.Extensions.AI"</c>,
    /// matching MEAI 10.5.0's <c>OpenTelemetryConsts.DefaultSourceName</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MEAI's <see cref="Microsoft.Extensions.AI.OpenTelemetryChatClient"/> emits the
    /// same <c>gen_ai.client.token.usage</c> histogram for <c>input</c> and <c>output</c>
    /// token types under a meter named by its <c>sourceName</c> constructor parameter
    /// (default <c>"Experimental.Microsoft.Extensions.AI"</c>). For the OpenTelemetry SDK
    /// to aggregate Needlr's <c>cache_read</c> / <c>reasoning</c> measurements into the
    /// same metric stream as MEAI's <c>input</c> / <c>output</c> measurements, the meter
    /// names MUST match. If the host configures MEAI with a custom <c>sourceName</c>
    /// (e.g. <c>UseOpenTelemetry(sourceName: "MyApp.GenAI")</c>), set this property to
    /// the same value:
    /// <code>
    /// .UsingAgentFramework(af => af
    ///     .ConfigureMetrics(o => o.GenAiMeterName = "MyApp.GenAI"))
    /// </code>
    /// </para>
    /// <para>
    /// This property is independent of <see cref="MeterName"/> — that property scopes
    /// Needlr-shape agent metrics (<c>agent.run.*</c>, <c>agent.tool.*</c>, etc.) which
    /// have no upstream cohabitation requirement, while this property scopes the OTel
    /// gen_ai semantic-convention histogram that Needlr shares with MEAI.
    /// </para>
    /// </remarks>
    public string GenAiMeterName { get; set; } = "Experimental.Microsoft.Extensions.AI";

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
