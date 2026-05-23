using System.Diagnostics.Metrics;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="GenAiTokenMetrics"/> in isolation. Verifies the histogram
/// instrument shape (name, unit, description, type, bucket boundaries) exactly matches
/// MEAI's <c>OpenTelemetryChatClient</c> so the OpenTelemetry SDK
/// <c>MetricStreamIdentity</c> matches and both writers aggregate cleanly into one
/// metric stream. Verifies the skip-zero contract, the long-to-int clamp, the meter
/// name configuration knob, and the optional-tag-omission rules.
/// </summary>
public sealed class GenAiTokenMetricsTests
{
    [Fact]
    public void RecordTokenUsage_NonZeroCount_EmitsHistogramSampleWithExpectedTags()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });

        var tags = new GenAiTokenUsageTags(
            RequestModel: "gpt-4.1",
            ResponseModel: "gpt-4.1-2025-04-14",
            ProviderName: "azure_openai",
            ServerAddress: "my-resource.openai.azure.com",
            ServerPort: 443);
        metrics.RecordTokenUsage("cache_read", 3000, tags);

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.Equal(3000, sample.Value);
        Assert.Equal("cache_read", sample.Tags["gen_ai.token.type"]);
        Assert.Equal("chat", sample.Tags["gen_ai.operation.name"]);
        Assert.Equal("gpt-4.1", sample.Tags["gen_ai.request.model"]);
        Assert.Equal("gpt-4.1-2025-04-14", sample.Tags["gen_ai.response.model"]);
        Assert.Equal("azure_openai", sample.Tags["gen_ai.provider.name"]);
        Assert.Equal("my-resource.openai.azure.com", sample.Tags["server.address"]);
        Assert.Equal(443, sample.Tags["server.port"]);
    }

    [Fact]
    public void RecordTokenUsage_ZeroCount_EmitsNothing()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });

        metrics.RecordTokenUsage("cache_read", 0, new GenAiTokenUsageTags());

        Assert.Empty(capture.IntMeasurements("gen_ai.client.token.usage"));
    }

    [Fact]
    public void RecordTokenUsage_NegativeCount_EmitsNothing()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });

        metrics.RecordTokenUsage("reasoning", -42, new GenAiTokenUsageTags());

        Assert.Empty(capture.IntMeasurements("gen_ai.client.token.usage"));
    }

    [Fact]
    public void RecordTokenUsage_NullOptionalTags_OmitsConditionalTags_KeepsProviderTagWithNullValue()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });

        metrics.RecordTokenUsage("reasoning", 50, new GenAiTokenUsageTags());

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.Equal("reasoning", sample.Tags["gen_ai.token.type"]);
        Assert.Equal("chat", sample.Tags["gen_ai.operation.name"]);
        Assert.False(sample.Tags.ContainsKey("gen_ai.request.model"));
        Assert.False(sample.Tags.ContainsKey("gen_ai.response.model"));
        Assert.False(sample.Tags.ContainsKey("server.address"));
        Assert.False(sample.Tags.ContainsKey("server.port"));
        Assert.True(sample.Tags.ContainsKey("gen_ai.provider.name"));
        Assert.Null(sample.Tags["gen_ai.provider.name"]);
    }

    [Fact]
    public void RecordTokenUsage_ServerPortNullButAddressSet_OmitsPortNotAddress()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });

        metrics.RecordTokenUsage(
            "cache_read",
            10,
            new GenAiTokenUsageTags(ServerAddress: "api.example.com", ServerPort: null));

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.Equal("api.example.com", sample.Tags["server.address"]);
        Assert.False(sample.Tags.ContainsKey("server.port"));
    }

    [Fact]
    public void RecordTokenUsage_TokenCountOverflowsInt32_ClampedToInt32MaxValue()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });

        metrics.RecordTokenUsage("reasoning", (long)int.MaxValue + 1L, new GenAiTokenUsageTags());

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.Equal(int.MaxValue, sample.Value);
    }

    [Fact]
    public void RecordTokenUsage_NullOrEmptyTokenType_Throws()
    {
        using var metrics = new GenAiTokenMetrics();

        Assert.Throws<ArgumentNullException>(() =>
            metrics.RecordTokenUsage(null!, 100, new GenAiTokenUsageTags()));
        Assert.Throws<ArgumentException>(() =>
            metrics.RecordTokenUsage(string.Empty, 100, new GenAiTokenUsageTags()));
    }

    [Fact]
    public void DefaultMeterName_IsExperimentalMicrosoftExtensionsAI()
    {
        var listener = new MeterListener();
        var meterNamesSeen = new List<string>();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "gen_ai.client.token.usage")
                meterNamesSeen.Add(instrument.Meter.Name);
        };
        listener.Start();

        using var metrics = new GenAiTokenMetrics();

        Assert.Contains("Experimental.Microsoft.Extensions.AI", meterNamesSeen);
    }

    [Fact]
    public void ConfiguredMeterName_OverridesDefault()
    {
        var listener = new MeterListener();
        var meterNamesSeen = new List<string>();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "gen_ai.client.token.usage")
                meterNamesSeen.Add(instrument.Meter.Name);
        };
        listener.Start();

        var customName = $"Custom.GenAI.{Guid.NewGuid():N}";
        using var metrics = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = customName });

        Assert.Contains(customName, meterNamesSeen);
    }

    [Fact]
    public void Histogram_HasName_genAiClientTokenUsage()
    {
        var listener = new MeterListener();
        Instrument? captured = null;
        var meterName = $"NexusLabs.Tests.{Guid.NewGuid():N}";
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == meterName)
                captured = instrument;
        };
        listener.Start();

        using var metrics = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });

        Assert.NotNull(captured);
        Assert.Equal("gen_ai.client.token.usage", captured!.Name);
    }

    [Fact]
    public void Histogram_HasUnit_TokensPerCurlyBraces()
    {
        var listener = new MeterListener();
        Instrument? captured = null;
        var meterName = $"NexusLabs.Tests.{Guid.NewGuid():N}";
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == meterName)
                captured = instrument;
        };
        listener.Start();

        using var metrics = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });

        Assert.NotNull(captured);
        Assert.Equal("{token}", captured!.Unit);
    }

    [Fact]
    public void Histogram_HasDescription_MatchingMeai()
    {
        var listener = new MeterListener();
        Instrument? captured = null;
        var meterName = $"NexusLabs.Tests.{Guid.NewGuid():N}";
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == meterName)
                captured = instrument;
        };
        listener.Start();

        using var metrics = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });

        Assert.NotNull(captured);
        Assert.Equal("Measures number of input and output tokens used", captured!.Description);
    }

    [Fact]
    public void Histogram_IsHistogramOfInt_NotLongOrDouble()
    {
        var listener = new MeterListener();
        Instrument? captured = null;
        var meterName = $"NexusLabs.Tests.{Guid.NewGuid():N}";
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == meterName)
                captured = instrument;
        };
        listener.Start();

        using var metrics = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });

        Assert.NotNull(captured);
        Assert.IsType<Histogram<int>>(captured);
    }

    [Fact]
    public void Histogram_HasExplicitBucketBoundaries_MatchingMeai()
    {
        Assert.Equal(
            new[] { 1, 4, 16, 64, 256, 1_024, 4_096, 16_384, 65_536, 262_144, 1_048_576, 4_194_304, 16_777_216, 67_108_864 },
            GenAiTokenMetrics.InstrumentBucketBoundaries);
    }

    [Fact]
    public void Dispose_DisposesUnderlyingMeter_NoEmissionAfterwards()
    {
        using var capture = new MetricCapture(out var meterName);
        var metrics = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });

        metrics.Dispose();

        metrics.RecordTokenUsage("cache_read", 100, new GenAiTokenUsageTags());

        Assert.Empty(capture.IntMeasurements("gen_ai.client.token.usage"));
    }

    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<RecordedMeasurement<int>> _ints = [];
        private readonly string _meterName;

        public MetricCapture(out string meterName)
        {
            _meterName = $"NexusLabs.Needlr.AgentFramework.Tests.{Guid.NewGuid():N}";
            meterName = _meterName;

            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == _meterName)
                    listener.EnableMeasurementEvents(instrument);
            };

            _listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
            {
                _ints.Add(new RecordedMeasurement<int>(instrument.Name, measurement, ToDictionary(tags)));
            });

            _listener.Start();
        }

        public IEnumerable<RecordedMeasurement<int>> IntMeasurements(string instrumentName) =>
            _ints.Where(m => m.InstrumentName == instrumentName);

        public void Dispose() => _listener.Dispose();

        private static Dictionary<string, object?> ToDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var dict = new Dictionary<string, object?>(tags.Length);
            foreach (var tag in tags)
                dict[tag.Key] = tag.Value;
            return dict;
        }
    }

    private sealed record RecordedMeasurement<T>(
        string InstrumentName,
        T Value,
        Dictionary<string, object?> Tags);
}
