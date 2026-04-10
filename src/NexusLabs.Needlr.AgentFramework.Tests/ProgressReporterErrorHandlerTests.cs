using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class ProgressReporterErrorHandlerTests
{
    [Fact]
    public void NullHandler_OnSinkException_DoesNotThrow()
    {
        var handler = new NullProgressReporterErrorHandler();
        var sink = new NoopSink();
        var evt = new WorkflowStartedEvent(
            DateTimeOffset.UtcNow, "wf-1", null, null, 0, 1);
        var ex = new InvalidOperationException("boom");

        handler.OnSinkException(sink, evt, ex);
    }

    [Fact]
    public void UsingAgentFramework_RegistersErrorHandler()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .BuildServiceProvider(config);

        var handler = sp.GetService<IProgressReporterErrorHandler>();

        Assert.NotNull(handler);
    }

    private sealed class NoopSink : IProgressSink
    {
        public ValueTask OnEventAsync(IProgressEvent progressEvent, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
