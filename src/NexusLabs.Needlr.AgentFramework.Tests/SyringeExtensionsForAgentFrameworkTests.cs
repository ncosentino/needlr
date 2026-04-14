using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class SyringeExtensionsForAgentFrameworkTests
{
    [Fact]
    public void Create_WithWorkflowIdOnly_UsesDiRegisteredSinksAsDefaults()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChat = new Mock<IChatClient>();
        var sink = new InlineSink();

        // Manually register a sink in DI to verify the default path picks it up.
        // In a real app, auto-discovery does this automatically.
        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object))
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IProgressSink>(sink);
            })
            .BuildServiceProvider(config);

        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
        var reporter = progressFactory.Create("default-wf");

        var evt = new WorkflowStartedEvent(
            DateTimeOffset.UtcNow, "default-wf", null, null, 0, 1);
        reporter.Report(evt);

        // The DI-registered sink must have received exactly one event.
        var received = Assert.Single(sink.Events);
        Assert.Same(evt, received);
    }

    [Fact]
    public void Create_WithExplicitSinks_DoesNotUseDefaultSinks()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChat = new Mock<IChatClient>();
        var defaultSink = new InlineSink();
        var explicitSink = new InlineSink();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object))
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IProgressSink>(defaultSink);
            })
            .BuildServiceProvider(config);

        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
        var reporter = progressFactory.Create("explicit-wf", [explicitSink]);

        var evt = new WorkflowStartedEvent(
            DateTimeOffset.UtcNow, "explicit-wf", null, null, 0, 1);
        reporter.Report(evt);

        // The explicit sink receives the event.
        var received = Assert.Single(explicitSink.Events);
        Assert.Same(evt, received);

        // The DI-registered default sink does NOT receive the event
        // because the per-orchestration overload bypasses defaults entirely.
        Assert.Empty(defaultSink.Events);
    }

    [Fact]
    public void Create_WithNoSinksRegistered_ReturnsNullReporter()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChat = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object))
            .BuildServiceProvider(config);

        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
        var reporter = progressFactory.Create("empty-wf");

        // With no sinks, Create returns the null reporter (zero-overhead no-op).
        Assert.Same(NullProgressReporter.Instance, reporter);
    }

    // --- Helper types ---

    /// <summary>
    /// Inline sink for tests — not registered in DI via auto-discovery.
    /// </summary>
    [DoNotAutoRegister]
    private sealed class InlineSink : IProgressSink
    {
        public List<IProgressEvent> Events { get; } = [];

        public ValueTask OnEventAsync(IProgressEvent progressEvent, CancellationToken cancellationToken)
        {
            Events.Add(progressEvent);
            return ValueTask.CompletedTask;
        }
    }
}
