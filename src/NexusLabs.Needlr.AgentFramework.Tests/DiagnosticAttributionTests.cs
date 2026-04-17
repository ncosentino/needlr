using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests verifying diagnostic attribution: every <see cref="ToolCallDiagnostics"/> and
/// <see cref="ChatCompletionDiagnostics"/> record carries the correct <c>AgentName</c>,
/// nested builders restore correctly, and metrics include agent dimensions.
/// </summary>
public class DiagnosticAttributionTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // -------------------------------------------------------------------------
    // Change 1: ToolCallDiagnostics.AgentName property
    // -------------------------------------------------------------------------

    [Fact]
    public void ToolCallDiagnostics_AgentName_DefaultsToNull()
    {
        var diag = MakeToolCall(0, "ReadFile");
        Assert.Null(diag.AgentName);
    }

    [Fact]
    public void ToolCallDiagnostics_AgentName_CanBeSetViaInit()
    {
        var diag = MakeToolCall(0, "ReadFile") with { AgentName = "ColdReader" };
        Assert.Equal("ColdReader", diag.AgentName);
    }

    [Fact]
    public void ToolCallDiagnostics_AgentName_SetViaObjectInitializer()
    {
        var diag = new ToolCallDiagnostics(
            Sequence: 0,
            ToolName: "Edit",
            Duration: TimeSpan.FromMilliseconds(50),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            CustomMetrics: null)
        {
            AgentName = "RevisionWriter"
        };

        Assert.Equal("RevisionWriter", diag.AgentName);
    }

    // -------------------------------------------------------------------------
    // Change 7: Builder stack-safety for nested runs
    // -------------------------------------------------------------------------

    [Fact]
    public void NestedStartNew_RestoresOuterBuilderOnDispose()
    {
        using var outer = AgentRunDiagnosticsBuilder.StartNew("OuterAgent");
        Assert.Same(outer, AgentRunDiagnosticsBuilder.GetCurrent());

        using (var inner = AgentRunDiagnosticsBuilder.StartNew("InnerAgent"))
        {
            Assert.Same(inner, AgentRunDiagnosticsBuilder.GetCurrent());
            Assert.Equal("InnerAgent", inner.AgentName);
        }

        // After inner disposes, outer is restored
        Assert.Same(outer, AgentRunDiagnosticsBuilder.GetCurrent());
        Assert.Equal("OuterAgent", outer.AgentName);
    }

    [Fact]
    public void NestedStartNew_ParentAgentName_ReflectsOuterAgent()
    {
        using var outer = AgentRunDiagnosticsBuilder.StartNew("ParentAgent");
        using var inner = AgentRunDiagnosticsBuilder.StartNew("ChildAgent");

        Assert.Equal("ParentAgent", inner.ParentAgentName);
        Assert.Null(outer.ParentAgentName);
    }

    [Fact]
    public void TripleNested_RestoresCorrectly()
    {
        using var a = AgentRunDiagnosticsBuilder.StartNew("A");
        using var b = AgentRunDiagnosticsBuilder.StartNew("B");
        using var c = AgentRunDiagnosticsBuilder.StartNew("C");

        Assert.Equal("C", AgentRunDiagnosticsBuilder.GetCurrent()!.AgentName);
        Assert.Equal("B", c.ParentAgentName);

        c.Dispose();
        Assert.Equal("B", AgentRunDiagnosticsBuilder.GetCurrent()!.AgentName);

        b.Dispose();
        Assert.Equal("A", AgentRunDiagnosticsBuilder.GetCurrent()!.AgentName);
    }

    // -------------------------------------------------------------------------
    // Change 3/4: IterativeAgentLoop wiring (via builder)
    // -------------------------------------------------------------------------

    [Fact]
    public void Builder_AddToolCall_WithAgentName_PreservesAttribution()
    {
        using var builder = AgentRunDiagnosticsBuilder.StartNew("IterativeAgent");

        builder.AddToolCall(MakeToolCall(0, "EditFile") with { AgentName = builder.AgentName });
        builder.AddToolCall(MakeToolCall(1, "ReadFile") with { AgentName = builder.AgentName });

        var result = builder.Build();
        Assert.Equal(2, result.ToolCalls.Count);
        Assert.All(result.ToolCalls, tc =>
            Assert.Equal("IterativeAgent", tc.AgentName));
    }

    [Fact]
    public void Builder_AddChatCompletion_WithAgentName_PreservesAttribution()
    {
        using var builder = AgentRunDiagnosticsBuilder.StartNew("IterativeAgent");

        builder.AddChatCompletion(MakeCompletion(0) with { AgentName = builder.AgentName });

        var result = builder.Build();
        Assert.Single(result.ChatCompletions);
        Assert.Equal("IterativeAgent", result.ChatCompletions[0].AgentName);
    }

    // -------------------------------------------------------------------------
    // Multi-agent attribution after aggregation
    // -------------------------------------------------------------------------

    [Fact]
    public void MultiAgent_FlattenedToolCalls_RetainCorrectAttribution()
    {
        // Simulate two agents producing diagnostics
        IAgentRunDiagnostics agent1Diag;
        IAgentRunDiagnostics agent2Diag;

        using (var builder1 = AgentRunDiagnosticsBuilder.StartNew("ColdReader"))
        {
            builder1.AddToolCall(MakeToolCall(0, "ReadFile") with { AgentName = "ColdReader" });
            builder1.AddToolCall(MakeToolCall(1, "WebSearch") with { AgentName = "ColdReader" });
            agent1Diag = builder1.Build();
        }

        using (var builder2 = AgentRunDiagnosticsBuilder.StartNew("RevisionWriter"))
        {
            builder2.AddToolCall(MakeToolCall(0, "EditFile") with { AgentName = "RevisionWriter" });
            agent2Diag = builder2.Build();
        }

        // Flatten — simulates what AggregateAgentRunDiagnostics does
        var allToolCalls = agent1Diag.ToolCalls
            .Concat(agent2Diag.ToolCalls)
            .ToList();

        Assert.Equal(3, allToolCalls.Count);

        // Every tool call must be unambiguously attributable
        Assert.All(allToolCalls, tc => Assert.NotNull(tc.AgentName));

        var coldReaderCalls = allToolCalls.Where(tc => tc.AgentName == "ColdReader").ToList();
        var revisionWriterCalls = allToolCalls.Where(tc => tc.AgentName == "RevisionWriter").ToList();

        Assert.Equal(2, coldReaderCalls.Count);
        Assert.Single(revisionWriterCalls);
        Assert.Equal("EditFile", revisionWriterCalls[0].ToolName);
    }

    [Fact]
    public void MultiAgent_FlattenedChatCompletions_RetainCorrectAttribution()
    {
        IAgentRunDiagnostics agent1Diag;
        IAgentRunDiagnostics agent2Diag;

        using (var builder1 = AgentRunDiagnosticsBuilder.StartNew("Agent1"))
        {
            builder1.AddChatCompletion(MakeCompletion(0) with { AgentName = "Agent1" });
            agent1Diag = builder1.Build();
        }

        using (var builder2 = AgentRunDiagnosticsBuilder.StartNew("Agent2"))
        {
            builder2.AddChatCompletion(MakeCompletion(0) with { AgentName = "Agent2" });
            builder2.AddChatCompletion(MakeCompletion(1) with { AgentName = "Agent2" });
            agent2Diag = builder2.Build();
        }

        var allCompletions = agent1Diag.ChatCompletions
            .Concat(agent2Diag.ChatCompletions)
            .ToList();

        Assert.Equal(3, allCompletions.Count);
        Assert.All(allCompletions, cc => Assert.NotNull(cc.AgentName));

        Assert.Single(allCompletions, cc => cc.AgentName == "Agent1");
        Assert.Equal(2, allCompletions.Count(cc => cc.AgentName == "Agent2"));
    }

    // -------------------------------------------------------------------------
    // Change 10: IAgentMetrics agent dimension
    // -------------------------------------------------------------------------

    [Fact]
    public void AgentMetrics_RecordToolCall_WithAgentName_DoesNotThrow()
    {
        var metrics = new AgentMetrics();
        metrics.RecordToolCall("ReadFile", TimeSpan.FromMilliseconds(50), true, agentName: "TestAgent");
        metrics.RecordToolCall("ReadFile", TimeSpan.FromMilliseconds(50), false, agentName: null);
    }

    [Fact]
    public void AgentMetrics_RecordChatCompletion_WithAgentName_DoesNotThrow()
    {
        var metrics = new AgentMetrics();
        metrics.RecordChatCompletion("gpt-4", TimeSpan.FromMilliseconds(200), true, agentName: "TestAgent");
        metrics.RecordChatCompletion("unknown", TimeSpan.FromMilliseconds(50), false, agentName: null);
    }

    [Fact]
    public void AgentMetrics_RecordToolCall_WithAgentName_EmitsAgentTag()
    {
        using var listener = new System.Diagnostics.Metrics.MeterListener();
        var capturedTags = new List<KeyValuePair<string, object?>>();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "NexusLabs.Needlr.AgentFramework" && instrument.Name == "agent.tool.completed")
                meterListener.EnableMeasurementEvents(instrument);
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            foreach (var tag in tags)
                capturedTags.Add(tag);
        });

        listener.Start();

        var metrics = new AgentMetrics();
        metrics.RecordToolCall("ReadFile", TimeSpan.FromMilliseconds(50), true, agentName: "MyAgent");

        Assert.Contains(capturedTags, t => t.Key == "agent_name" && (string?)t.Value == "MyAgent");
    }

    // -------------------------------------------------------------------------
    // ToolCallCollector
    // -------------------------------------------------------------------------

    [Fact]
    public void NullToolCallCollector_DrainReturnsEmpty()
    {
        var collector = NullToolCallCollector.Instance;
        Assert.Empty(collector.DrainToolCalls());
    }

    [Fact]
    public void ToolCallCollectorHolder_DefaultsToNull()
    {
        var holder = new ToolCallCollectorHolder();
        Assert.Empty(holder.DrainToolCalls());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ChatCompletionDiagnostics MakeCompletion(int sequence) =>
        new(Sequence: sequence,
            Model: "test-model",
            Tokens: new TokenUsage(10, 20, 30, 0, 0),
            InputMessageCount: 1,
            Duration: TimeSpan.FromMilliseconds(10),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow);

    private static ToolCallDiagnostics MakeToolCall(int sequence, string name = "tool") =>
        new(Sequence: sequence,
            ToolName: name,
            Duration: TimeSpan.FromMilliseconds(5),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            CustomMetrics: null);
}
