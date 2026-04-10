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
}
