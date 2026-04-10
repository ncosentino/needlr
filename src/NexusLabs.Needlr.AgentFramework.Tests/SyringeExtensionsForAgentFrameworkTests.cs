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
    public void ProgressReporterFactory_ResolvedBeforeAgentFactory_StillCapturesSyringeSinkTypes()
    {
        CountingSyringeSink.Received = 0;

        var config = new ConfigurationBuilder().Build();
        var mockChat = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object)
                .AddProgressSink<CountingSyringeSink>())
            .BuildServiceProvider(config);

        // Intentionally resolve IProgressReporterFactory BEFORE IAgentFactory.
        // With the previous closure-capture wiring, syringeSinkTypes would still
        // be null at this point and the CountingSyringeSink would be silently dropped.
        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();

        // Use overload that falls back to DI-registered defaults.
        var reporter = progressFactory.Create("wf-race");

        var evt = new WorkflowStartedEvent(
            DateTimeOffset.UtcNow, "wf-race", null, null, 0, 1);
        reporter.Report(evt);

        Assert.Equal(1, CountingSyringeSink.Received);
    }

    [Fact]
    public void AddProgressSink_EndToEnd_SinkReceivesEventsFromCreatedReporter()
    {
        E2EProgressSink.Clear();

        var config = new ConfigurationBuilder().Build();
        var mockChat = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object)
                .AddProgressSink<E2EProgressSink>())
            .BuildServiceProvider(config);

        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
        var reporter = progressFactory.Create("e2e-wf");

        var evt1 = new WorkflowStartedEvent(
            DateTimeOffset.UtcNow, "e2e-wf", null, null, 0, 1);
        var evt2 = new WorkflowCompletedEvent(
            DateTimeOffset.UtcNow, "e2e-wf", null, null, 0, 2, true, null, TimeSpan.FromMilliseconds(10));

        reporter.Report(evt1);
        reporter.Report(evt2);

        Assert.Equal(2, E2EProgressSink.Received.Count);
        Assert.Same(evt1, E2EProgressSink.Received[0]);
        Assert.Same(evt2, E2EProgressSink.Received[1]);
    }

    [Fact]
    public void AddProgressSink_MultipleSinks_AllReceiveEvents()
    {
        E2EProgressSink.Clear();
        SecondaryE2ESink.Clear();

        var config = new ConfigurationBuilder().Build();
        var mockChat = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object)
                .AddProgressSink<E2EProgressSink>()
                .AddProgressSink<SecondaryE2ESink>())
            .BuildServiceProvider(config);

        var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
        var reporter = progressFactory.Create("multi-wf");

        var evt = new WorkflowStartedEvent(
            DateTimeOffset.UtcNow, "multi-wf", null, null, 0, 1);

        reporter.Report(evt);

        // Both sinks must receive the event — verifies the syringe's sink-list
        // path stacks correctly (no first-wins).
        var firstReceived = Assert.Single(E2EProgressSink.Received);
        var secondReceived = Assert.Single(SecondaryE2ESink.Received);
        Assert.Same(evt, firstReceived);
        Assert.Same(evt, secondReceived);
    }

    [DoNotAutoRegister]
    public sealed class CountingSyringeSink : IProgressSink
    {
        public static int Received;

        public ValueTask OnEventAsync(IProgressEvent progressEvent, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Received);
            return ValueTask.CompletedTask;
        }
    }

    [DoNotAutoRegister]
    public sealed class E2EProgressSink : IProgressSink
    {
        private static readonly List<IProgressEvent> _received = new();
        private static readonly object _lock = new();

        public static IReadOnlyList<IProgressEvent> Received
        {
            get { lock (_lock) return _received.ToList(); }
        }

        public static void Clear()
        {
            lock (_lock) _received.Clear();
        }

        public ValueTask OnEventAsync(IProgressEvent progressEvent, CancellationToken cancellationToken)
        {
            lock (_lock) _received.Add(progressEvent);
            return ValueTask.CompletedTask;
        }
    }

    [DoNotAutoRegister]
    public sealed class SecondaryE2ESink : IProgressSink
    {
        private static readonly List<IProgressEvent> _received = new();
        private static readonly object _lock = new();

        public static IReadOnlyList<IProgressEvent> Received
        {
            get { lock (_lock) return _received.ToList(); }
        }

        public static void Clear()
        {
            lock (_lock) _received.Clear();
        }

        public ValueTask OnEventAsync(IProgressEvent progressEvent, CancellationToken cancellationToken)
        {
            lock (_lock) _received.Add(progressEvent);
            return ValueTask.CompletedTask;
        }
    }
}
