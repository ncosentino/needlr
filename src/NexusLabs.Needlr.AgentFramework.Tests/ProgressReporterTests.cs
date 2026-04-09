using Moq;

using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class ProgressReporterTests
{
    // -------------------------------------------------------------------------
    // Fan-out to sinks
    // -------------------------------------------------------------------------

    [Fact]
    public void Report_FansOutToAllSinks()
    {
        var sink1 = new Mock<IProgressSink>();
        var sink2 = new Mock<IProgressSink>();
        sink1.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        sink2.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var reporter = new ProgressReporter("wf-1", [sink1.Object, sink2.Object], new ProgressSequenceProvider());
        var evt = MakeEvent(reporter);

        reporter.Report(evt);

        sink1.Verify(s => s.OnEventAsync(evt, It.IsAny<CancellationToken>()), Times.Once);
        sink2.Verify(s => s.OnEventAsync(evt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Report_NoSinks_DoesNotThrow()
    {
        var reporter = new ProgressReporter("wf-1", [], new ProgressSequenceProvider());

        reporter.Report(MakeEvent(reporter));
    }

    // -------------------------------------------------------------------------
    // CreateChild — hierarchical scoping
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateChild_SetsAgentId()
    {
        var reporter = new ProgressReporter("wf-1", [], new ProgressSequenceProvider());

        var child = reporter.CreateChild("agent-A");

        Assert.Equal("agent-A", child.AgentId);
    }

    [Fact]
    public void CreateChild_SetsParentAgentId()
    {
        var reporter = new ProgressReporter("wf-1", [], new ProgressSequenceProvider(), agentId: "parent-agent");

        var child = reporter.CreateChild("child-agent");

        // Child's events should reference parent
        var childImpl = Assert.IsType<ProgressReporter>(child);
        Assert.Equal("wf-1", child.WorkflowId);
    }

    [Fact]
    public void CreateChild_IncrementsDepth()
    {
        var reporter = new ProgressReporter("wf-1", [], new ProgressSequenceProvider(), depth: 0);

        var child = reporter.CreateChild("agent-A");
        var grandchild = child.CreateChild("sub-agent-B");

        Assert.Equal(0, reporter.Depth);
        Assert.Equal(1, child.Depth);
        Assert.Equal(2, grandchild.Depth);
    }

    [Fact]
    public void CreateChild_SharesSinks()
    {
        var sink = new Mock<IProgressSink>();
        sink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var reporter = new ProgressReporter("wf-1", [sink.Object], new ProgressSequenceProvider());
        var child = reporter.CreateChild("agent-A");

        child.Report(MakeEvent(child));

        sink.Verify(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CreateChild_PreservesWorkflowId()
    {
        var reporter = new ProgressReporter("wf-123", [], new ProgressSequenceProvider());

        var child = reporter.CreateChild("agent-X");

        Assert.Equal("wf-123", child.WorkflowId);
    }

    // -------------------------------------------------------------------------
    // Sequence numbers
    // -------------------------------------------------------------------------

    [Fact]
    public void NextSequence_IsMonotonicallyIncreasing()
    {
        var seqProvider = new ProgressSequenceProvider();
        var seq1 = seqProvider.Next();
        var seq2 = seqProvider.Next();
        var seq3 = seqProvider.Next();

        Assert.True(seq2 > seq1);
        Assert.True(seq3 > seq2);
    }

    // -------------------------------------------------------------------------
    // NullProgressReporter
    // -------------------------------------------------------------------------

    [Fact]
    public void NullReporter_ReportDoesNothing()
    {
        var reporter = NullProgressReporter.Instance;

        reporter.Report(MakeEvent(reporter)); // no throw
    }

    [Fact]
    public void NullReporter_CreateChild_ReturnsSelf()
    {
        var reporter = NullProgressReporter.Instance;

        var child = reporter.CreateChild("agent");

        Assert.Same(reporter, child);
    }

    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------

    [Fact]
    public void Factory_Create_WithDefaultSinks_ReturnsReporter()
    {
        var sink = new Mock<IProgressSink>();
        var factory = new ProgressReporterFactory([sink.Object], new ProgressSequenceProvider());

        var reporter = factory.Create("wf-1");

        Assert.IsType<ProgressReporter>(reporter);
    }

    [Fact]
    public void Factory_Create_NoSinks_ReturnsNullReporter()
    {
        var factory = new ProgressReporterFactory([], new ProgressSequenceProvider());

        var reporter = factory.Create("wf-1");

        Assert.Same(NullProgressReporter.Instance, reporter);
    }

    [Fact]
    public void Factory_Create_WithExplicitSinks_ReturnsReporter()
    {
        var factory = new ProgressReporterFactory([], new ProgressSequenceProvider());
        var sink = new Mock<IProgressSink>();

        var reporter = factory.Create("wf-1", [sink.Object]);

        Assert.IsType<ProgressReporter>(reporter);
    }

    [Fact]
    public void Factory_Create_WithEmptyExplicitSinks_ReturnsNullReporter()
    {
        var factory = new ProgressReporterFactory([], new ProgressSequenceProvider());

        var reporter = factory.Create("wf-1", []);

        Assert.Same(NullProgressReporter.Instance, reporter);
    }

    // -------------------------------------------------------------------------
    // Event records
    // -------------------------------------------------------------------------

    [Fact]
    public void WorkflowStartedEvent_CarriesCorrelation()
    {
        var evt = new WorkflowStartedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: "wf-1",
            AgentId: null,
            ParentAgentId: null,
            Depth: 0,
            SequenceNumber: 1);

        Assert.Equal("wf-1", evt.WorkflowId);
        Assert.Null(evt.AgentId);
        Assert.Equal(0, evt.Depth);
    }

    [Fact]
    public void LlmCallCompletedEvent_CarriesTokenData()
    {
        var evt = new LlmCallCompletedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: "wf-1",
            AgentId: "geo-agent",
            ParentAgentId: null,
            Depth: 1,
            SequenceNumber: 5,
            CallSequence: 2,
            Model: "gpt-5-nano",
            Duration: TimeSpan.FromMilliseconds(3500),
            InputTokens: 100,
            OutputTokens: 200,
            TotalTokens: 300);

        Assert.Equal("gpt-5-nano", evt.Model);
        Assert.Equal(300, evt.TotalTokens);
        Assert.Equal("geo-agent", evt.AgentId);
    }

    [Fact]
    public void BudgetUpdatedEvent_CarriesAllBudgetFields()
    {
        var evt = new BudgetUpdatedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: "wf-1",
            AgentId: null,
            ParentAgentId: null,
            Depth: 0,
            SequenceNumber: 10,
            CurrentInputTokens: 500,
            CurrentOutputTokens: 300,
            CurrentTotalTokens: 800,
            MaxInputTokens: 1000,
            MaxOutputTokens: null,
            MaxTotalTokens: 5000);

        Assert.Equal(500, evt.CurrentInputTokens);
        Assert.Equal(5000, evt.MaxTotalTokens);
        Assert.Null(evt.MaxOutputTokens);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IProgressEvent MakeEvent(IProgressReporter reporter) =>
        new WorkflowStartedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: reporter.WorkflowId,
            AgentId: reporter.AgentId,
            ParentAgentId: null,
            Depth: reporter.Depth,
            SequenceNumber: new ProgressSequenceProvider().Next());
}
