using Moq;

using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class ChannelProgressReporterTests
{
    [Fact]
    public async Task Report_DeliversToSink()
    {
        var received = new List<IProgressEvent>();
        var sink = new Mock<IProgressSink>();
        sink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IProgressEvent, CancellationToken>((evt, _) => received.Add(evt))
            .Returns(ValueTask.CompletedTask);

        await using var reporter = new ChannelProgressReporter(
            "wf-1", [sink.Object], new ProgressSequenceProvider());

        var evt = new WorkflowStartedEvent(
            DateTimeOffset.UtcNow, "wf-1", null, null, 0, 1);

        reporter.Report(evt);

        // Give the consumer time to drain
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Single(received);
        Assert.Same(evt, received[0]);
    }

    [Fact]
    public async Task Report_IsNonBlocking()
    {
        var sink = new Mock<IProgressSink>();
        sink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Returns(async (IProgressEvent _, CancellationToken _) =>
            {
                await Task.Delay(500); // slow sink
            });

        await using var reporter = new ChannelProgressReporter(
            "wf-1", [sink.Object], new ProgressSequenceProvider());

        var before = DateTime.Now;
        reporter.Report(new WorkflowStartedEvent(
            DateTimeOffset.UtcNow, "wf-1", null, null, 0, 1));
        var after = DateTime.Now;

        // Report should return immediately — not wait for the slow sink
        Assert.True((after - before).TotalMilliseconds < 50);
    }

    [Fact]
    public async Task DisposeAsync_DrainsRemainingEvents()
    {
        var count = 0;
        var sink = new Mock<IProgressSink>();
        sink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IProgressEvent, CancellationToken>((_, _) => Interlocked.Increment(ref count))
            .Returns(ValueTask.CompletedTask);

        var reporter = new ChannelProgressReporter(
            "wf-1", [sink.Object], new ProgressSequenceProvider());

        for (int i = 0; i < 10; i++)
        {
            reporter.Report(new WorkflowStartedEvent(
                DateTimeOffset.UtcNow, "wf-1", null, null, 0, i));
        }

        await reporter.DisposeAsync();

        Assert.Equal(10, count);
    }

    [Fact]
    public async Task CreateChild_InheritsChannelBehavior()
    {
        var received = new List<IProgressEvent>();
        var sink = new Mock<IProgressSink>();
        sink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IProgressEvent, CancellationToken>((evt, _) => received.Add(evt))
            .Returns(ValueTask.CompletedTask);

        await using var reporter = new ChannelProgressReporter(
            "wf-1", [sink.Object], new ProgressSequenceProvider());

        var child = reporter.CreateChild("agent-A");

        Assert.Equal("agent-A", child.AgentId);
        Assert.Equal(1, child.Depth);
        Assert.Equal("wf-1", child.WorkflowId);
    }

    [Fact]
    public async Task NextSequence_Delegates()
    {
        await using var reporter = new ChannelProgressReporter(
            "wf-1", [], new ProgressSequenceProvider());

        var s1 = reporter.NextSequence();
        var s2 = reporter.NextSequence();

        Assert.True(s2 > s1);
    }
}
