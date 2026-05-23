using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Default <see cref="IGenAiTokenMetrics"/> implementation. Owns a dedicated
/// <see cref="Meter"/> whose name is configurable via
/// <see cref="AgentFrameworkMetricsOptions.GenAiMeterName"/>.
/// </summary>
/// <remarks>
/// <para>
/// The histogram is created with the same name (<c>gen_ai.client.token.usage</c>),
/// type (<see cref="Histogram{T}"/> of <see cref="int"/>), unit (<c>{token}</c>),
/// description, and explicit bucket boundaries that MEAI's
/// <see cref="Microsoft.Extensions.AI.OpenTelemetryChatClient"/> uses (per
/// <c>OpenTelemetryConsts.GenAI.Client.TokenUsage</c> in
/// <see href="https://github.com/dotnet/extensions/blob/v10.5.0/src/Libraries/Microsoft.Extensions.AI/OpenTelemetryConsts.cs"/>).
/// This shape parity is required for the OpenTelemetry SDK's <c>MetricStreamIdentity</c>
/// to consider Needlr's measurements and MEAI's measurements part of the same metric
/// stream rather than colliding into duplicate-instrument warnings.
/// </para>
/// <para>
/// Token counts are passed as <see cref="long"/> at the API surface (matching the type
/// of <see cref="Microsoft.Extensions.AI.UsageDetails.InputTokenCount"/> et al.) but
/// recorded as <see cref="int"/> after a saturating clamp at <see cref="int.MaxValue"/>.
/// At single-call granularity values exceeding 2.1 billion tokens are not realistic.
/// </para>
/// </remarks>
[DoNotAutoRegister]
internal sealed class GenAiTokenMetrics : IGenAiTokenMetrics, IDisposable
{
    /// <summary>
    /// The exact instrument name MEAI's
    /// <see cref="Microsoft.Extensions.AI.OpenTelemetryChatClient"/> uses for the token
    /// usage histogram. Sourced verbatim from MEAI's <c>OpenTelemetryConsts.GenAI.Client.TokenUsage.Name</c>.
    /// </summary>
    internal const string InstrumentName = "gen_ai.client.token.usage";

    /// <summary>
    /// The exact instrument unit MEAI uses. Sourced verbatim from MEAI's
    /// <c>OpenTelemetryConsts.TokensUnit</c>.
    /// </summary>
    internal const string InstrumentUnit = "{token}";

    /// <summary>
    /// The exact instrument description MEAI uses. Sourced verbatim from MEAI's
    /// <c>OpenTelemetryConsts.GenAI.Client.TokenUsage.Description</c>.
    /// </summary>
    internal const string InstrumentDescription = "Measures number of input and output tokens used";

    /// <summary>
    /// The exact explicit bucket boundaries MEAI uses. Sourced verbatim from MEAI's
    /// <c>OpenTelemetryConsts.GenAI.Client.TokenUsage.ExplicitBucketBoundaries</c>.
    /// </summary>
    internal static readonly int[] InstrumentBucketBoundaries =
    [
        1, 4, 16, 64, 256, 1_024, 4_096, 16_384,
        65_536, 262_144, 1_048_576, 4_194_304, 16_777_216, 67_108_864,
    ];

    private readonly Meter _meter;
    private readonly Histogram<int> _tokenUsage;

    public GenAiTokenMetrics() : this(new AgentFrameworkMetricsOptions()) { }

    public GenAiTokenMetrics(AgentFrameworkMetricsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _meter = new Meter(options.GenAiMeterName);
        _tokenUsage = _meter.CreateHistogram<int>(
            name: InstrumentName,
            unit: InstrumentUnit,
            description: InstrumentDescription,
            advice: new InstrumentAdvice<int> { HistogramBucketBoundaries = InstrumentBucketBoundaries });
    }

    /// <inheritdoc />
    public void RecordTokenUsage(string tokenType, long tokenCount, GenAiTokenUsageTags tags)
    {
        ArgumentException.ThrowIfNullOrEmpty(tokenType);

        if (tokenCount <= 0)
            return;

        var sample = (int)Math.Min(tokenCount, int.MaxValue);

        var tagList = new TagList
        {
            { "gen_ai.token.type", tokenType },
            { "gen_ai.operation.name", string.IsNullOrEmpty(tags.OperationName) ? "chat" : tags.OperationName },
        };

        if (tags.RequestModel is not null)
            tagList.Add("gen_ai.request.model", tags.RequestModel);

        // gen_ai.provider.name is added UNCONDITIONALLY (even when null) to match MEAI's
        // OpenTelemetryChatClient.AddMetricTags exactly. Label-set parity is required for
        // the OpenTelemetry SDK MetricStreamIdentity to consider Needlr's measurements and
        // MEAI's measurements part of the same stream rather than splitting into two.
        tagList.Add("gen_ai.provider.name", tags.ProviderName);

        if (tags.ServerAddress is not null)
        {
            tagList.Add("server.address", tags.ServerAddress);
            if (tags.ServerPort is int port)
                tagList.Add("server.port", port);
        }

        if (tags.ResponseModel is not null)
            tagList.Add("gen_ai.response.model", tags.ResponseModel);

        _tokenUsage.Record(sample, tagList);
    }

    /// <summary>Disposes the underlying <see cref="Meter"/>.</summary>
    public void Dispose() => _meter.Dispose();
}
