namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Records measurements on the OpenTelemetry <c>gen_ai.client.token.usage</c> histogram
/// for token-type categories that <see cref="Microsoft.Extensions.AI.OpenTelemetryChatClient"/>
/// does not emit itself (today: <c>cache_read</c> and <c>reasoning</c>).
/// </summary>
/// <remarks>
/// <para>
/// MEAI's <see cref="Microsoft.Extensions.AI.OpenTelemetryChatClient"/> emits the
/// <c>gen_ai.client.token.usage</c> histogram per the OpenTelemetry GenAI semantic
/// conventions, but only for <c>gen_ai.token.type = "input"</c> and <c>"output"</c>.
/// Implementations of this interface emit additional measurements on the same histogram
/// for token-type categories that exist in <see cref="Microsoft.Extensions.AI.UsageDetails"/>
/// (<see cref="Microsoft.Extensions.AI.UsageDetails.CachedInputTokenCount"/>,
/// <see cref="Microsoft.Extensions.AI.UsageDetails.ReasoningTokenCount"/>) but are not
/// part of the OTel semantic-convention <c>gen_ai.token.type</c> enum today.
/// </para>
/// <para>
/// Implementations MUST never emit <c>input</c> or <c>output</c> — those are MEAI's
/// responsibility and duplicate emission would silently double recorded values.
/// </para>
/// <para>
/// Implementations MUST skip emission when the token count is zero or
/// negative — zero samples are not meaningful and pathological negative counts must not
/// be recorded.
/// </para>
/// <para>
/// For the two writers (Needlr's middleware and MEAI's <see cref="Microsoft.Extensions.AI.OpenTelemetryChatClient"/>)
/// to aggregate into a single OpenTelemetry <c>MetricStreamIdentity</c>, the implementation's
/// underlying <see cref="System.Diagnostics.Metrics.Meter"/> name MUST match the <c>sourceName</c>
/// passed to MEAI's <see cref="Microsoft.Extensions.AI.OpenTelemetryChatClient"/> constructor.
/// Configure via <see cref="AgentFrameworkMetricsOptions.GenAiMeterName"/>.
/// </para>
/// </remarks>
public interface IGenAiTokenMetrics
{
    /// <summary>
    /// Records a single sample on the <c>gen_ai.client.token.usage</c> histogram with
    /// the supplied <paramref name="tokenType"/> as the <c>gen_ai.token.type</c> tag value
    /// and the supplied <paramref name="tags"/> attached as additional measurement tags.
    /// </summary>
    /// <param name="tokenType">
    /// Value for the <c>gen_ai.token.type</c> tag (e.g. <c>"cache_read"</c>, <c>"reasoning"</c>).
    /// Must not be <see langword="null"/> or empty.
    /// </param>
    /// <param name="tokenCount">
    /// The number of tokens to record. Implementations MUST skip emission when this is
    /// less than or equal to zero.
    /// </param>
    /// <param name="tags">
    /// Additional metric tags. Tags whose values are <see langword="null"/> are omitted from
    /// the recorded measurement.
    /// </param>
    void RecordTokenUsage(string tokenType, long tokenCount, GenAiTokenUsageTags tags);
}
