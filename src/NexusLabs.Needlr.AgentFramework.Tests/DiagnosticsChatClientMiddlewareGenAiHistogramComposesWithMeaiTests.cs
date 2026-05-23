using System.Diagnostics.Metrics;

using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// The most critical test in the gen_ai histogram suite: composes Needlr's
/// <see cref="DiagnosticsChatClientMiddleware"/> ON TOP OF MEAI's
/// <see cref="OpenTelemetryChatClient"/> in the production
/// <see cref="DelegatingChatClient"/> topology and asserts that all four token-type
/// series (<c>input</c>, <c>output</c>, <c>cache_read</c>, <c>reasoning</c>) appear on
/// the shared <c>gen_ai.client.token.usage</c> histogram with no double-counting of
/// <c>input</c> or <c>output</c>. This is the production correctness contract.
/// </summary>
/// <remarks>
/// Note: a <see cref="MeterListener"/> proves both writers publish instruments and
/// records measurement events. The OpenTelemetry SDK aggregation behaviour
/// (<c>MetricStreamIdentity</c>) that determines whether two writers' measurements
/// combine into one stream or split into two is a separate concern, validated by the
/// instrument-shape tests in <c>GenAiTokenMetricsTests</c> (description, unit,
/// type, bucket boundaries all match MEAI exactly, which is the necessary and
/// sufficient condition for SDK-level stream aggregation).
/// </remarks>
public sealed class DiagnosticsChatClientMiddlewareGenAiHistogramComposesWithMeaiTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task NeedlrMiddlewareAtopMeaiOpenTelemetryChatClient_AllFourTokenTypesPresent_NoDoubleCounting()
    {
        var meterName = $"NexusLabs.Cohabitation.{Guid.NewGuid():N}";

        using var capture = new IntCapture(meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });

        var mockReal = new Mock<IChatClient>();
        mockReal
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")])
            {
                ModelId = "test-model",
                Usage = new UsageDetails
                {
                    InputTokenCount = 100,
                    OutputTokenCount = 50,
                    CachedInputTokenCount = 20,
                    ReasoningTokenCount = 10,
                },
            });

        using var meaiWrapped = new OpenTelemetryChatClient(mockReal.Object, sourceName: meterName);
        var needlrMiddleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);

        await needlrMiddleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, meaiWrapped, _ct);

        var samples = capture.Measurements.ToList();

        var inputSamples = samples.Where(s => (string?)s.Tags.GetValueOrDefault("gen_ai.token.type") == "input").ToList();
        var outputSamples = samples.Where(s => (string?)s.Tags.GetValueOrDefault("gen_ai.token.type") == "output").ToList();
        var cacheReadSamples = samples.Where(s => (string?)s.Tags.GetValueOrDefault("gen_ai.token.type") == "cache_read").ToList();
        var reasoningSamples = samples.Where(s => (string?)s.Tags.GetValueOrDefault("gen_ai.token.type") == "reasoning").ToList();

        Assert.Single(inputSamples);
        Assert.Equal(100, inputSamples[0].Value);

        Assert.Single(outputSamples);
        Assert.Equal(50, outputSamples[0].Value);

        Assert.Single(cacheReadSamples);
        Assert.Equal(20, cacheReadSamples[0].Value);

        Assert.Single(reasoningSamples);
        Assert.Equal(10, reasoningSamples[0].Value);

        Assert.Equal(4, samples.Count);
    }

    private sealed class IntCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<Recorded> _samples = [];

        public IntCapture(string meterName)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterName && instrument.Name == "gen_ai.client.token.usage")
                    listener.EnableMeasurementEvents(instrument);
            };

            _listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
            {
                _samples.Add(new Recorded(measurement, ToDictionary(tags)));
            });

            _listener.Start();
        }

        public IEnumerable<Recorded> Measurements => _samples;

        public void Dispose() => _listener.Dispose();

        private static Dictionary<string, object?> ToDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var dict = new Dictionary<string, object?>(tags.Length);
            foreach (var tag in tags)
                dict[tag.Key] = tag.Value;
            return dict;
        }
    }

    private sealed record Recorded(int Value, Dictionary<string, object?> Tags);
}
