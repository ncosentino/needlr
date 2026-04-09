using System.Reflection;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
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
    // Helpers
    // -------------------------------------------------------------------------

    private static (IServiceProvider sp, Mock<IChatClient> mockChat) BuildServiceProvider(
        bool useDiagnostics = false)
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
            });

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af =>
            {
                af = af.Configure(opts => opts.ChatClientFactory = _ => mockChat.Object);
                if (useDiagnostics) af = af.UsingDiagnostics();
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
