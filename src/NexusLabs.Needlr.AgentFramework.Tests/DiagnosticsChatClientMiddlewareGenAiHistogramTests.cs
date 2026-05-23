using System.Diagnostics.Metrics;

using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests that <see cref="DiagnosticsChatClientMiddleware"/> emits <c>cache_read</c> and
/// <c>reasoning</c> samples on the OpenTelemetry <c>gen_ai.client.token.usage</c>
/// histogram when the chat response carries non-zero
/// <see cref="UsageDetails.CachedInputTokenCount"/> or
/// <see cref="UsageDetails.ReasoningTokenCount"/>, across all three emission sites
/// (non-streaming success, streaming success, streaming failure). Verifies the anti-
/// regression contract that the middleware does NOT emit <c>input</c> or <c>output</c>
/// samples (those are MEAI's responsibility), the skip-zero contract, the tag schema
/// matching MEAI's <c>AddMetricTags</c>, the absence of <c>error.type</c> on the failure
/// path (label-set parity required for Prometheus), and the middleware-level short-
/// circuit that prevents <see cref="ChatClientMetadata"/> resolution on the common
/// no-cache/no-reasoning path.
/// </summary>
public sealed class DiagnosticsChatClientMiddlewareGenAiHistogramTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_CachedInputTokensPositive_EmitsCacheReadOnGenAiHistogram()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);
        var mockInner = MockChatClientReturningUsage(new UsageDetails
        {
            InputTokenCount = 5000,
            OutputTokenCount = 200,
            CachedInputTokenCount = 3000,
        });

        await middleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct);

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.Equal(3000, sample.Value);
        Assert.Equal("cache_read", sample.Tags["gen_ai.token.type"]);
    }

    [Fact]
    public async Task HandleAsync_ReasoningTokensPositive_EmitsReasoningOnGenAiHistogram()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);
        var mockInner = MockChatClientReturningUsage(new UsageDetails
        {
            InputTokenCount = 100,
            OutputTokenCount = 500,
            ReasoningTokenCount = 350,
        });

        await middleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct);

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.Equal(350, sample.Value);
        Assert.Equal("reasoning", sample.Tags["gen_ai.token.type"]);
    }

    [Fact]
    public async Task HandleAsync_BothCachedAndReasoningPositive_EmitsTwoSeparateSamples()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);
        var mockInner = MockChatClientReturningUsage(new UsageDetails
        {
            InputTokenCount = 5000,
            OutputTokenCount = 600,
            CachedInputTokenCount = 3000,
            ReasoningTokenCount = 350,
        });

        await middleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct);

        var samples = capture.IntMeasurements("gen_ai.client.token.usage").ToList();
        Assert.Equal(2, samples.Count);
        Assert.Single(samples, s => (string?)s.Tags["gen_ai.token.type"] == "cache_read" && s.Value == 3000);
        Assert.Single(samples, s => (string?)s.Tags["gen_ai.token.type"] == "reasoning" && s.Value == 350);
    }

    [Fact]
    public async Task HandleAsync_CachedInputTokensZero_DoesNotEmitCacheRead()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);
        var mockInner = MockChatClientReturningUsage(new UsageDetails
        {
            InputTokenCount = 100,
            OutputTokenCount = 500,
            CachedInputTokenCount = 0,
            ReasoningTokenCount = 50,
        });

        await middleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct);

        Assert.DoesNotContain(
            capture.IntMeasurements("gen_ai.client.token.usage"),
            s => (string?)s.Tags["gen_ai.token.type"] == "cache_read");
    }

    [Fact]
    public async Task HandleAsync_ReasoningTokensZero_DoesNotEmitReasoning()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);
        var mockInner = MockChatClientReturningUsage(new UsageDetails
        {
            InputTokenCount = 100,
            OutputTokenCount = 500,
            CachedInputTokenCount = 50,
            ReasoningTokenCount = 0,
        });

        await middleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct);

        Assert.DoesNotContain(
            capture.IntMeasurements("gen_ai.client.token.usage"),
            s => (string?)s.Tags["gen_ai.token.type"] == "reasoning");
    }

    [Fact]
    public async Task HandleAsync_DoesNotEmitInputOrOutput_AntiRegressionForMeaiCohabitation()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);
        var mockInner = MockChatClientReturningUsage(new UsageDetails
        {
            InputTokenCount = 100,
            OutputTokenCount = 500,
            CachedInputTokenCount = 30,
            ReasoningTokenCount = 70,
        });

        await middleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct);

        Assert.DoesNotContain(
            capture.IntMeasurements("gen_ai.client.token.usage"),
            s => (string?)s.Tags["gen_ai.token.type"] == "input");
        Assert.DoesNotContain(
            capture.IntMeasurements("gen_ai.client.token.usage"),
            s => (string?)s.Tags["gen_ai.token.type"] == "output");
    }

    [Fact]
    public async Task HandleStreamingAsync_CachedInputTokensPositive_EmitsCacheReadOnGenAiHistogram()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);
        var mockInner = MockChatClientReturningStreamUsage(new UsageDetails
        {
            InputTokenCount = 4000,
            OutputTokenCount = 100,
            CachedInputTokenCount = 2500,
        });

        await foreach (var _ in middleware.HandleStreamingAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct)) { }

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.Equal(2500, sample.Value);
        Assert.Equal("cache_read", sample.Tags["gen_ai.token.type"]);
    }

    [Fact]
    public async Task HandleStreamingAsync_ReasoningTokensPositive_EmitsReasoningOnGenAiHistogram()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);
        var mockInner = MockChatClientReturningStreamUsage(new UsageDetails
        {
            InputTokenCount = 100,
            OutputTokenCount = 800,
            ReasoningTokenCount = 600,
        });

        await foreach (var _ in middleware.HandleStreamingAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct)) { }

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.Equal(600, sample.Value);
        Assert.Equal("reasoning", sample.Tags["gen_ai.token.type"]);
    }

    [Fact]
    public async Task HandleStreamingAsync_BothZero_EmitsNothing()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);
        var mockInner = MockChatClientReturningStreamUsage(new UsageDetails
        {
            InputTokenCount = 100,
            OutputTokenCount = 500,
        });

        await foreach (var _ in middleware.HandleStreamingAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct)) { }

        Assert.Empty(capture.IntMeasurements("gen_ai.client.token.usage"));
    }

    [Fact]
    public async Task HandleAsync_FailurePath_NoUsage_EmitsNothing()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);
        var mockInner = new Mock<IChatClient>();
        mockInner
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct));

        Assert.Empty(capture.IntMeasurements("gen_ai.client.token.usage"));
    }

    [Fact]
    public async Task HandleStreamingAsync_FailureAfterPartialUsage_StillEmitsObservedCacheReadAndReasoning()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);

        var firstUpdate = new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = [new TextContent("partial"), new UsageContent(new UsageDetails
            {
                InputTokenCount = 100,
                OutputTokenCount = 50,
                CachedInputTokenCount = 80,
                ReasoningTokenCount = 30,
            })],
        };

        var mockInner = new Mock<IChatClient>();
        mockInner
            .Setup(c => c.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Returns(ThrowingStream(firstUpdate, new InvalidOperationException("stream broke")));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in middleware.HandleStreamingAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct)) { }
        });

        var samples = capture.IntMeasurements("gen_ai.client.token.usage").ToList();
        Assert.Equal(2, samples.Count);
        Assert.Single(samples, s => (string?)s.Tags["gen_ai.token.type"] == "cache_read" && s.Value == 80);
        Assert.Single(samples, s => (string?)s.Tags["gen_ai.token.type"] == "reasoning" && s.Value == 30);
    }

    [Fact]
    public async Task FailurePath_DoesNotAttachErrorTypeTag_LabelSetParityWithMeai()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);

        var firstUpdate = new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = [new UsageContent(new UsageDetails
            {
                CachedInputTokenCount = 50,
                ReasoningTokenCount = 25,
            })],
        };

        var mockInner = new Mock<IChatClient>();
        mockInner
            .Setup(c => c.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Returns(ThrowingStream(firstUpdate, new InvalidOperationException("boom")));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in middleware.HandleStreamingAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct)) { }
        });

        var samples = capture.IntMeasurements("gen_ai.client.token.usage").ToList();
        Assert.NotEmpty(samples);
        foreach (var sample in samples)
            Assert.False(sample.Tags.ContainsKey("error.type"));
    }

    [Fact]
    public async Task Tags_MatchExpectedSchema_OperationNameAndModels()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);
        var mockInner = MockChatClientReturningUsage(new UsageDetails
        {
            CachedInputTokenCount = 100,
        }, modelId: "claude-sonnet-4");

        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: new ChatOptions { ModelId = "claude-sonnet-4-latest" },
            mockInner.Object,
            _ct);

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.Equal("cache_read", sample.Tags["gen_ai.token.type"]);
        Assert.Equal("chat", sample.Tags["gen_ai.operation.name"]);
        Assert.Equal("claude-sonnet-4-latest", sample.Tags["gen_ai.request.model"]);
        Assert.Equal("claude-sonnet-4", sample.Tags["gen_ai.response.model"]);
    }

    [Fact]
    public async Task Tags_ProviderNameAndServerAddress_PopulatedFromChatClientMetadata()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);

        var mockInner = MockChatClientReturningUsage(
            new UsageDetails { CachedInputTokenCount = 100 },
            metadata: new ChatClientMetadata("anthropic", new Uri("https://api.anthropic.com:443")));

        await middleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct);

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.Equal("anthropic", sample.Tags["gen_ai.provider.name"]);
        Assert.Equal("api.anthropic.com", sample.Tags["server.address"]);
        Assert.Equal(443, sample.Tags["server.port"]);
    }

    [Fact]
    public async Task Tags_NullChatClientMetadata_OmitsServerTags_KeepsProviderTagWithNullValue()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);
        var mockInner = MockChatClientReturningUsage(
            new UsageDetails { CachedInputTokenCount = 100 },
            metadata: null);

        await middleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct);

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.True(sample.Tags.ContainsKey("gen_ai.provider.name"));
        Assert.Null(sample.Tags["gen_ai.provider.name"]);
        Assert.False(sample.Tags.ContainsKey("server.address"));
        Assert.False(sample.Tags.ContainsKey("server.port"));
    }

    [Fact]
    public async Task Tags_RequestModelFallsBackToChatClientMetadataDefaultModelId_WhenChatOptionsModelIdNull()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);

        var mockInner = MockChatClientReturningUsage(
            new UsageDetails { CachedInputTokenCount = 100 },
            metadata: new ChatClientMetadata("openai", new Uri("https://api.openai.com:443"), defaultModelId: "gpt-4.1-default"));

        await middleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct);

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.Equal("gpt-4.1-default", sample.Tags["gen_ai.request.model"]);
    }

    [Fact]
    public async Task Tags_RequestModelFromChatOptionsTakesPrecedence_OverChatClientMetadataDefaultModelId()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);

        var mockInner = MockChatClientReturningUsage(
            new UsageDetails { CachedInputTokenCount = 100 },
            metadata: new ChatClientMetadata("openai", new Uri("https://api.openai.com:443"), defaultModelId: "gpt-4.1-default"));

        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: new ChatOptions { ModelId = "gpt-4.1-explicit" },
            mockInner.Object,
            _ct);

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.Equal("gpt-4.1-explicit", sample.Tags["gen_ai.request.model"]);
    }

    [Fact]
    public async Task Middleware_NullGenAiTokenMetrics_DoesNotEmit_NoExceptions()
    {
        using var capture = new MetricCapture(out var meterName);
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: null);
        var mockInner = MockChatClientReturningUsage(new UsageDetails
        {
            CachedInputTokenCount = 100,
            ReasoningTokenCount = 50,
        });

        await middleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct);

        Assert.Empty(capture.IntMeasurements("gen_ai.client.token.usage"));
    }

    [Fact]
    public async Task Tags_ChatClientMetadataBehindMeaiOpenTelemetryChatClient_StillResolves()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);

        var inner = new FakeChatClientWithMetadata(
            new ChatClientMetadata("openai", new Uri("https://api.openai.com:443")),
            new UsageDetails { CachedInputTokenCount = 200 });
        using var meaiWrapper = new OpenTelemetryChatClient(inner);

        await middleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, meaiWrapper, _ct);

        var sample = Assert.Single(capture.IntMeasurements("gen_ai.client.token.usage"));
        Assert.Equal("openai", sample.Tags["gen_ai.provider.name"]);
        Assert.Equal("api.openai.com", sample.Tags["server.address"]);
    }

    [Fact]
    public async Task Middleware_BothCacheAndReasoningZero_DoesNotResolveChatClientMetadata()
    {
        using var capture = new MetricCapture(out var meterName);
        using var genAi = new GenAiTokenMetrics(new AgentFrameworkMetricsOptions { GenAiMeterName = meterName });
        var middleware = new DiagnosticsChatClientMiddleware(genAiTokenMetrics: genAi);
        var mockInner = MockChatClientReturningUsage(new UsageDetails
        {
            InputTokenCount = 100,
            OutputTokenCount = 500,
        });

        await middleware.HandleAsync([new ChatMessage(ChatRole.User, "hi")], options: null, mockInner.Object, _ct);

        mockInner.Verify(c => c.GetService(typeof(ChatClientMetadata), null), Times.Never);
    }

    private static Mock<IChatClient> MockChatClientReturningUsage(
        UsageDetails usage,
        string modelId = "test-model",
        ChatClientMetadata? metadata = null)
    {
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")])
            {
                ModelId = modelId,
                Usage = usage,
            });
        if (metadata is not null)
        {
            mock.Setup(c => c.GetService(typeof(ChatClientMetadata), null)).Returns(metadata);
        }
        return mock;
    }

    private static Mock<IChatClient> MockChatClientReturningStreamUsage(UsageDetails usage)
    {
        var update = new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            ModelId = "test-model",
            Contents = [new TextContent("ok"), new UsageContent(usage)],
        };

        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable([update]));
        return mock;
    }

    private static async IAsyncEnumerable<T> AsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
            yield return item;
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> ThrowingStream(ChatResponseUpdate first, Exception toThrow)
    {
        yield return first;
        await Task.CompletedTask;
        throw toThrow;
    }

    private sealed class FakeChatClientWithMetadata(ChatClientMetadata metadata, UsageDetails usage) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]) { ModelId = "test-model", Usage = usage });

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            if (serviceType == typeof(ChatClientMetadata))
                return metadata;
            return null;
        }

        public void Dispose() { }
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
