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
    public async Task CreateChild_SetsPropertiesAndSharesParentChannel()
    {
        var received = new List<IProgressEvent>();
        var receivedLock = new object();
        var sink = new Mock<IProgressSink>();
        sink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IProgressEvent, CancellationToken>((evt, _) =>
            {
                lock (receivedLock) received.Add(evt);
            })
            .Returns(ValueTask.CompletedTask);

        var reporter = new ChannelProgressReporter(
            "wf-1", [sink.Object], new ProgressSequenceProvider());

        var child = reporter.CreateChild("agent-A");

        // Properties carry correct scope.
        Assert.Equal("agent-A", child.AgentId);
        Assert.Equal(1, child.Depth);
        Assert.Equal("wf-1", child.WorkflowId);

        // Behavior: events reported via the CHILD must reach the parent's sink
        // (shared channel). This is the load-bearing assertion — the previous
        // test only checked properties and would pass even if the child spawned
        // its own dropped channel.
        var evtViaChild = new WorkflowStartedEvent(
            DateTimeOffset.UtcNow, "wf-1", "agent-A", null, 1, 100);
        var evtViaParent = new WorkflowStartedEvent(
            DateTimeOffset.UtcNow, "wf-1", null, null, 0, 101);

        child.Report(evtViaChild);
        reporter.Report(evtViaParent);

        await reporter.DisposeAsync();

        Assert.Equal(2, received.Count);
        Assert.Contains(received, e => ReferenceEquals(e, evtViaChild));
        Assert.Contains(received, e => ReferenceEquals(e, evtViaParent));
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

    [Fact]
    public async Task Consumer_SinkThrows_InvokesErrorHandler_AndContinues()
    {
        var boom = new InvalidOperationException("kaboom");
        var throwCountByEvent = new Dictionary<int, bool> { [0] = true, [1] = false, [2] = false };

        var throwingSink = new Mock<IProgressSink>();
        throwingSink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Returns<IProgressEvent, CancellationToken>((evt, _) =>
            {
                var seq = (int)evt.SequenceNumber;
                if (throwCountByEvent.TryGetValue(seq, out var shouldThrow) && shouldThrow)
                    throw boom;
                return ValueTask.CompletedTask;
            });

        var goodSinkReceived = new List<IProgressEvent>();
        var goodSink = new Mock<IProgressSink>();
        goodSink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IProgressEvent, CancellationToken>((evt, _) =>
            {
                lock (goodSinkReceived) goodSinkReceived.Add(evt);
            })
            .Returns(ValueTask.CompletedTask);

        var handler = new RecordingChannelErrorHandler();

        var reporter = new ChannelProgressReporter(
            "wf-1",
            [throwingSink.Object, goodSink.Object],
            new ProgressSequenceProvider(),
            handler);

        reporter.Report(new WorkflowStartedEvent(DateTimeOffset.UtcNow, "wf-1", null, null, 0, 0));
        reporter.Report(new WorkflowStartedEvent(DateTimeOffset.UtcNow, "wf-1", null, null, 0, 1));
        reporter.Report(new WorkflowStartedEvent(DateTimeOffset.UtcNow, "wf-1", null, null, 0, 2));

        await reporter.DisposeAsync();

        Assert.Single(handler.Records);
        Assert.Same(throwingSink.Object, handler.Records[0].Sink);
        Assert.Equal(0L, handler.Records[0].Event.SequenceNumber);
        Assert.Same(boom, handler.Records[0].Exception);

        Assert.Equal(3, goodSinkReceived.Count);
        Assert.Equal(new long[] { 0, 1, 2 }, goodSinkReceived.Select(e => e.SequenceNumber));
    }

    private sealed class RecordingChannelErrorHandler : IProgressReporterErrorHandler
    {
        private readonly List<(IProgressSink Sink, IProgressEvent Event, Exception Exception)> _records = new();

        public IReadOnlyList<(IProgressSink Sink, IProgressEvent Event, Exception Exception)> Records => _records;

        public void OnSinkException(IProgressSink sink, IProgressEvent progressEvent, Exception exception)
        {
            lock (_records)
            {
                _records.Add((sink, progressEvent, exception));
            }
        }
    }
}
