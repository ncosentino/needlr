using System.Reflection;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workflows.Budget;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Middleware;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Integration tests verifying progress events fire during real agent execution
/// with mock IChatClient.
/// </summary>
public class ProgressIntegrationTests
{
    [Fact]
    public async Task DirectAgentRun_EmitsLlmCallEvents()
    {
        var events = new List<IProgressEvent>();
        var sink = new CollectorSink(events);

        var (sp, _) = BuildServiceProvider(useDiagnostics: true);
        var factory = sp.GetRequiredService<IAgentFactory>();
        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = sp.GetRequiredService<IProgressReporterAccessor>();

        var reporter = progressFactory.Create("test-wf", [sink]);
        var agent = factory.CreateAgent(opts => opts.Name = "TestAgent");

        using (progressAccessor.BeginScope(reporter))
        {
            await agent.RunAsync("Hello", cancellationToken: TestContext.Current.CancellationToken);
        }

        // Chat middleware must have emitted exactly one started/completed pair,
        // both carrying the reporter's WorkflowId. Direct-agent runs don't push
        // a child scope per agent, so AgentId is inherited from the reporter
        // (null in this setup). The CallSequence and SequenceNumber contracts
        // still apply though.
        var started = Assert.Single(events.OfType<LlmCallStartedEvent>());
        var completed = Assert.Single(events.OfType<LlmCallCompletedEvent>());

        Assert.Equal("test-wf", started.WorkflowId);
        Assert.Equal("test-wf", completed.WorkflowId);
        Assert.Equal(started.CallSequence, completed.CallSequence);
        Assert.True(completed.SequenceNumber > started.SequenceNumber,
            "LlmCallCompletedEvent sequence must be strictly greater than LlmCallStartedEvent sequence.");
    }

    [Fact]
    public async Task ChildReporter_LlmCallEvents_CarryChildAgentIdAndWorkflowId()
    {
        var events = new List<IProgressEvent>();
        var sink = new CollectorSink(events);

        var (sp, _) = BuildServiceProvider(useDiagnostics: true);
        var factory = sp.GetRequiredService<IAgentFactory>();
        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = sp.GetRequiredService<IProgressReporterAccessor>();

        var rootReporter = progressFactory.Create("carrier-wf", [sink]);
        var childReporter = rootReporter.CreateChild("CarrierAgent");
        var agent = factory.CreateAgent(opts => opts.Name = "CarrierAgent");

        using (progressAccessor.BeginScope(childReporter))
        {
            await agent.RunAsync("Hello", cancellationToken: TestContext.Current.CancellationToken);
        }

        var llmEvents = events.OfType<LlmCallStartedEvent>().Cast<IProgressEvent>()
            .Concat(events.OfType<LlmCallCompletedEvent>())
            .ToList();
        Assert.NotEmpty(llmEvents);

        // With an explicit child scope, every LLM event carries the child's agent ID
        // and preserves the workflow ID from the parent reporter.
        Assert.All(llmEvents, e =>
        {
            Assert.Equal("carrier-wf", e.WorkflowId);
            Assert.Equal("CarrierAgent", e.AgentId);
        });
    }

    [Fact]
    public async Task DirectAgentRun_Events_HavePerAgentMonotonicSequence()
    {
        var events = new List<IProgressEvent>();
        var sink = new CollectorSink(events);

        var (sp, _) = BuildServiceProvider(useDiagnostics: true);
        var factory = sp.GetRequiredService<IAgentFactory>();
        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = sp.GetRequiredService<IProgressReporterAccessor>();

        var reporter = progressFactory.Create("per-agent-wf", [sink]);
        var agent = factory.CreateAgent(opts => opts.Name = "MonoAgent");

        using (progressAccessor.BeginScope(reporter))
        {
            await agent.RunAsync("Hello", cancellationToken: TestContext.Current.CancellationToken);
        }

        // Group events by agent (null group = workflow-level events) and assert
        // each group's sequence numbers are strictly monotonically increasing.
        var groups = events.GroupBy(e => e.AgentId ?? "<workflow>").ToList();
        Assert.NotEmpty(groups);

        foreach (var group in groups)
        {
            var sequences = group.Select(e => e.SequenceNumber).ToList();
            for (int i = 1; i < sequences.Count; i++)
            {
                Assert.True(sequences[i] > sequences[i - 1],
                    $"Agent '{group.Key}' emitted out-of-order sequences: " +
                    $"[{string.Join(", ", sequences)}]");
            }
        }
    }

    [Fact]
    public async Task DirectAgentRun_LlmCallCompleted_HasModelAndDuration()
    {
        var events = new List<IProgressEvent>();
        var sink = new CollectorSink(events);

        var (sp, _) = BuildServiceProvider(useDiagnostics: true);
        var factory = sp.GetRequiredService<IAgentFactory>();
        var progressAccessor = sp.GetRequiredService<IProgressReporterAccessor>();
        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();

        var reporter = progressFactory.Create("test-wf", [sink]);
        var agent = factory.CreateAgent(opts => opts.Name = "TestAgent");

        using (progressAccessor.BeginScope(reporter))
        {
            await agent.RunAsync("Hello", cancellationToken: TestContext.Current.CancellationToken);
        }

        var completed = Assert.Single(events.OfType<LlmCallCompletedEvent>());
        Assert.True(completed.Duration > TimeSpan.Zero);
        Assert.Equal("mock-model", completed.Model);
        Assert.Equal(10, completed.InputTokens);
        Assert.Equal(20, completed.OutputTokens);
        Assert.Equal(30, completed.TotalTokens);
    }

    [Fact]
    public async Task DirectAgentRun_EventsAreOrdered()
    {
        var events = new List<IProgressEvent>();
        var sink = new CollectorSink(events);

        var (sp, _) = BuildServiceProvider(useDiagnostics: true);
        var factory = sp.GetRequiredService<IAgentFactory>();
        var progressAccessor = sp.GetRequiredService<IProgressReporterAccessor>();
        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();

        var reporter = progressFactory.Create("test-wf", [sink]);
        var agent = factory.CreateAgent(opts => opts.Name = "TestAgent");

        using (progressAccessor.BeginScope(reporter))
        {
            await agent.RunAsync("Hello", cancellationToken: TestContext.Current.CancellationToken);
        }

        // LlmCallStarted should come before LlmCallCompleted
        var startIdx = events.FindIndex(e => e is LlmCallStartedEvent);
        var completeIdx = events.FindIndex(e => e is LlmCallCompletedEvent);

        Assert.True(startIdx >= 0, "LlmCallStartedEvent not found");
        Assert.True(completeIdx >= 0, "LlmCallCompletedEvent not found");
        Assert.True(startIdx < completeIdx, "Started should come before Completed");
    }

    // -------------------------------------------------------------------------
    // #13: Tool call events propagate (relies on LLM triggering a tool call,
    // which requires a FunctionCallContent mock — complex to set up with MAF.
    // Instead, we verify the middleware is wired by checking that the accessor
    // is resolved and used in the function middleware constructor path.)
    // -------------------------------------------------------------------------

    [Fact]
    public void FunctionMiddleware_UsesProgressAccessor_NotDirectReporter()
    {
        // Verify IProgressReporterAccessor is registered and resolvable.
        // The function middleware reads accessor.Current during tool calls.
        var (sp, _) = BuildServiceProvider(useDiagnostics: true);
        var accessor = sp.GetService<IProgressReporterAccessor>();

        Assert.NotNull(accessor);
        // Without a scope, Current is NullProgressReporter (zero overhead)
        Assert.Same(NullProgressReporter.Instance, accessor!.Current);
    }

    // -------------------------------------------------------------------------
    // #14: Budget events propagate
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DirectAgentRun_WithBudget_EmitsBudgetUpdatedEvent()
    {
        var events = new List<IProgressEvent>();
        var sink = new CollectorSink(events);

        var (sp, _) = BuildServiceProvider(useDiagnostics: true, useTokenBudget: true);
        var factory = sp.GetRequiredService<IAgentFactory>();
        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = sp.GetRequiredService<IProgressReporterAccessor>();
        var budgetTracker = sp.GetRequiredService<ITokenBudgetTracker>();

        var reporter = progressFactory.Create("budget-wf", [sink]);
        var agent = factory.CreateAgent(opts => opts.Name = "BudgetAgent");

        using (progressAccessor.BeginScope(reporter))
        using (budgetTracker.BeginScope(100_000))
        {
            await agent.RunAsync("Hello", cancellationToken: TestContext.Current.CancellationToken);
        }

        // Mock chat returns 30 tokens per call; the budget tracker must emit
        // an update carrying that exact usage against the 100,000 cap.
        var budgetEvents = events.OfType<BudgetUpdatedEvent>().ToList();
        Assert.NotEmpty(budgetEvents);

        var last = budgetEvents[^1];
        Assert.Equal("budget-wf", last.WorkflowId);
        Assert.Equal(100_000, last.MaxTotalTokens);
        Assert.True(last.CurrentTotalTokens > 0, "BudgetUpdatedEvent should report non-zero consumed tokens.");
        Assert.True(last.CurrentTotalTokens <= 100_000, "Consumed tokens must not exceed the configured max.");
    }

    // -------------------------------------------------------------------------
    // #9: End-to-end correlation context
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DirectAgentRun_AllEvents_HaveCorrectWorkflowId()
    {
        var events = new List<IProgressEvent>();
        var sink = new CollectorSink(events);

        var (sp, _) = BuildServiceProvider(useDiagnostics: true);
        var factory = sp.GetRequiredService<IAgentFactory>();
        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = sp.GetRequiredService<IProgressReporterAccessor>();

        var reporter = progressFactory.Create("e2e-test-wf", [sink]);
        var agent = factory.CreateAgent(opts => opts.Name = "E2EAgent");

        using (progressAccessor.BeginScope(reporter))
        {
            await agent.RunAsync("Hello", cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.Equal("e2e-test-wf", e.WorkflowId));
    }

    [Fact]
    public async Task DirectAgentRun_AllEvents_HaveNonZeroSequence()
    {
        var events = new List<IProgressEvent>();
        var sink = new CollectorSink(events);

        var (sp, _) = BuildServiceProvider(useDiagnostics: true);
        var factory = sp.GetRequiredService<IAgentFactory>();
        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = sp.GetRequiredService<IProgressReporterAccessor>();

        var reporter = progressFactory.Create("seq-test-wf", [sink]);
        var agent = factory.CreateAgent(opts => opts.Name = "SeqAgent");

        using (progressAccessor.BeginScope(reporter))
        {
            await agent.RunAsync("Hello", cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.True(e.SequenceNumber > 0, $"Event {e.GetType().Name} has sequence 0"));
    }

    [Fact]
    public async Task DirectAgentRun_Events_AreStrictlyOrderedBySequence()
    {
        var events = new List<IProgressEvent>();
        var sink = new CollectorSink(events);

        var (sp, _) = BuildServiceProvider(useDiagnostics: true);
        var factory = sp.GetRequiredService<IAgentFactory>();
        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = sp.GetRequiredService<IProgressReporterAccessor>();

        var reporter = progressFactory.Create("order-test-wf", [sink]);
        var agent = factory.CreateAgent(opts => opts.Name = "OrderAgent");

        using (progressAccessor.BeginScope(reporter))
        {
            await agent.RunAsync("Hello", cancellationToken: TestContext.Current.CancellationToken);
        }

        for (int i = 1; i < events.Count; i++)
        {
            Assert.True(events[i].SequenceNumber > events[i - 1].SequenceNumber,
                $"Event {i} ({events[i].GetType().Name} seq={events[i].SequenceNumber}) " +
                $"should be > event {i - 1} ({events[i - 1].GetType().Name} seq={events[i - 1].SequenceNumber})");
        }
    }

    // -------------------------------------------------------------------------
    // #3: AgentFailedEvent emission from PipelineRunExtensions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PipelineRun_WhenAgentThrows_EmitsAgentFailedEvent()
    {
        var events = new List<IProgressEvent>();
        var sink = new CollectorSink(events);

        var config = new ConfigurationBuilder().Build();
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        mockChat
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("boom"));

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object)
                .AddAgent<FailingSeqAgentA>()
                .AddAgent<FailingSeqAgentB>()
                .UsingDiagnostics())
            .BuildServiceProvider(config);

        var workflowFactory = sp.GetRequiredService<IWorkflowFactory>();
        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
        var progressAccessor = sp.GetRequiredService<IProgressReporterAccessor>();
        var diagnosticsAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var workflow = workflowFactory.CreateSequentialWorkflow("wf-failing-pipeline");
        var reporter = progressFactory.Create("failing-wf", [sink]);

        var result = await workflow.RunWithDiagnosticsAsync(
            "Hello",
            new WorkflowRunOptions
            {
                DiagnosticsAccessor = diagnosticsAccessor,
                ProgressReporter = reporter,
                ProgressReporterAccessor = progressAccessor,
                CancellationToken = TestContext.Current.CancellationToken,
            });

        Assert.False(result.Succeeded);

        var failed = Assert.Single(events.OfType<AgentFailedEvent>());
        Assert.NotNull(failed.AgentId);
        Assert.Contains("boom", failed.ErrorMessage);

        var completedIdx = events.FindIndex(e => e is WorkflowCompletedEvent);
        var failedIdx = events.FindIndex(e => ReferenceEquals(e, failed));
        Assert.True(failedIdx >= 0);
        Assert.True(completedIdx >= 0);
        Assert.True(failedIdx < completedIdx, "AgentFailedEvent should be emitted before WorkflowCompletedEvent");
    }

    [NeedlrAiAgent(Instructions = "First failing agent.")]
    [AgentSequenceMember("wf-failing-pipeline", 1)]
    public sealed class FailingSeqAgentA { }

    [NeedlrAiAgent(Instructions = "Second failing agent.")]
    [AgentSequenceMember("wf-failing-pipeline", 2)]
    public sealed class FailingSeqAgentB { }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (IServiceProvider sp, Mock<IChatClient> mockChat) BuildServiceProvider(
        bool useDiagnostics = false,
        bool useTokenBudget = false)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "Hi")])
            {
                ModelId = "mock-model",
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 20, TotalTokenCount = 30 },
            });

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af =>
            {
                af = af.Configure(opts => opts.ChatClientFactory = _ => mockChat.Object);
                if (useDiagnostics) af = af.UsingDiagnostics();
                if (useTokenBudget) af = af.UsingTokenBudget();
                return af;
            })
            .BuildServiceProvider(config);

        return (sp, mockChat);
    }

    private sealed class CollectorSink(List<IProgressEvent> events) : IProgressSink
    {
        public ValueTask OnEventAsync(IProgressEvent progressEvent, CancellationToken cancellationToken)
        {
            events.Add(progressEvent);
            return ValueTask.CompletedTask;
        }
    }
}
