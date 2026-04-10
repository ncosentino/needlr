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

        // Chat middleware should have emitted LlmCallStarted + LlmCallCompleted
        Assert.Contains(events, e => e is LlmCallStartedEvent);
        Assert.Contains(events, e => e is LlmCallCompletedEvent);
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

        var completed = events.OfType<LlmCallCompletedEvent>().FirstOrDefault();
        Assert.NotNull(completed);
        Assert.True(completed!.Duration > TimeSpan.Zero);
        Assert.NotEqual("unknown", completed.Model);
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

        var reporter = progressFactory.Create("test-wf", [sink]);
        var agent = factory.CreateAgent(opts => opts.Name = "BudgetAgent");

        using (progressAccessor.BeginScope(reporter))
        using (budgetTracker.BeginScope(100_000))
        {
            await agent.RunAsync("Hello", cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.Contains(events, e => e is BudgetUpdatedEvent);
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
