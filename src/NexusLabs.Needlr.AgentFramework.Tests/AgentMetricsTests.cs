using System.Diagnostics.Metrics;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentMetricsTests
{
    // -------------------------------------------------------------------------
    // DI registration
    // -------------------------------------------------------------------------

    [Fact]
    public void UsingAgentFramework_RegistersIAgentMetrics()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .BuildServiceProvider(config);

        var metrics = sp.GetService<IAgentMetrics>();

        Assert.NotNull(metrics);
    }

    // -------------------------------------------------------------------------
    // Interface methods don't throw
    // -------------------------------------------------------------------------

    [Fact]
    public void RecordRunStarted_DoesNotThrow()
    {
        var metrics = new AgentMetrics();

        metrics.RecordRunStarted("TestAgent");
    }

    [Fact]
    public void RecordRunCompleted_DoesNotThrow()
    {
        var metrics = new AgentMetrics();
        var diag = CreateDiagnostics("TestAgent");

        metrics.RecordRunCompleted(diag);
    }

    [Fact]
    public void RecordToolCall_DoesNotThrow()
    {
        var metrics = new AgentMetrics();

        metrics.RecordToolCall("GetData", TimeSpan.FromMilliseconds(50), true);
        metrics.RecordToolCall("GetData", TimeSpan.FromMilliseconds(100), false);
    }

    [Fact]
    public void RecordChatCompletion_DoesNotThrow()
    {
        var metrics = new AgentMetrics();

        metrics.RecordChatCompletion("gpt-4", TimeSpan.FromMilliseconds(200), true);
        metrics.RecordChatCompletion("unknown", TimeSpan.FromMilliseconds(50), false);
    }

    // -------------------------------------------------------------------------
    // Meter emits correct instruments
    // -------------------------------------------------------------------------

    [Fact]
    public void Meter_EmitsRunStartedCounter()
    {
        using var listener = new MeterListener();
        long captured = 0;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "NexusLabs.Needlr.AgentFramework" && instrument.Name == "agent.run.started")
                meterListener.EnableMeasurementEvents(instrument);
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            captured += measurement;
        });

        listener.Start();

        var metrics = new AgentMetrics();
        metrics.RecordRunStarted("TestAgent");
        metrics.RecordRunStarted("TestAgent");

        listener.RecordObservableInstruments();

        Assert.Equal(2, captured);
    }

    [Fact]
    public void Meter_EmitsToolCallCounter()
    {
        using var listener = new MeterListener();
        long captured = 0;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "NexusLabs.Needlr.AgentFramework" && instrument.Name == "agent.tool.completed")
                meterListener.EnableMeasurementEvents(instrument);
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            captured += measurement;
        });

        listener.Start();

        var metrics = new AgentMetrics();
        metrics.RecordToolCall("GetData", TimeSpan.FromMilliseconds(50), true);

        Assert.Equal(1, captured);
    }

    [Fact]
    public void Meter_EmitsTokensUsedCounter()
    {
        using var listener = new MeterListener();
        long captured = 0;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "NexusLabs.Needlr.AgentFramework" && instrument.Name == "agent.tokens.used")
                meterListener.EnableMeasurementEvents(instrument);
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            captured += measurement;
        });

        listener.Start();

        var metrics = new AgentMetrics();
        metrics.RecordRunCompleted(CreateDiagnostics("Agent", inputTokens: 100, outputTokens: 200));

        // 100 input + 200 output
        Assert.Equal(300, captured);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IAgentRunDiagnostics CreateDiagnostics(
        string agentName,
        long inputTokens = 10,
        long outputTokens = 20) =>
        new AgentRunDiagnostics(
            AgentName: agentName,
            TotalDuration: TimeSpan.FromSeconds(1),
            AggregateTokenUsage: new TokenUsage(inputTokens, outputTokens, inputTokens + outputTokens, 0, 0),
            ChatCompletions: [],
            ToolCalls: [],
            TotalInputMessages: 1,
            TotalOutputMessages: 1,
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow);
}
