using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class ReducerNodeInvokedEventTests
{
    [Fact]
    public void Constructor_PreservesAllProperties()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromMilliseconds(42);

        var evt = new ReducerNodeInvokedEvent(
            Timestamp: timestamp,
            WorkflowId: "wf-1",
            AgentId: null,
            ParentAgentId: null,
            Depth: 0,
            SequenceNumber: 7,
            NodeId: "reducer-merge",
            GraphName: "research",
            BranchId: "fan-in",
            InputBranchCount: 3,
            Duration: duration);

        Assert.Equal(timestamp, evt.Timestamp);
        Assert.Equal("wf-1", evt.WorkflowId);
        Assert.Null(evt.AgentId);
        Assert.Null(evt.ParentAgentId);
        Assert.Equal(0, evt.Depth);
        Assert.Equal(7, evt.SequenceNumber);
        Assert.Equal("reducer-merge", evt.NodeId);
        Assert.Equal("research", evt.GraphName);
        Assert.Equal("fan-in", evt.BranchId);
        Assert.Equal(3, evt.InputBranchCount);
        Assert.Equal(duration, evt.Duration);
    }

    [Fact]
    public void Implements_IProgressEvent()
    {
        var evt = new ReducerNodeInvokedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: "wf-1",
            AgentId: null,
            ParentAgentId: null,
            Depth: 0,
            SequenceNumber: 1,
            NodeId: "node",
            GraphName: null,
            BranchId: null,
            InputBranchCount: 2,
            Duration: TimeSpan.Zero);

        Assert.IsAssignableFrom<IProgressEvent>(evt);
    }

    [Fact]
    public void NullableProperties_DefaultToNull()
    {
        var evt = new ReducerNodeInvokedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: "wf-1",
            AgentId: null,
            ParentAgentId: null,
            Depth: 0,
            SequenceNumber: 1,
            NodeId: "node",
            GraphName: null,
            BranchId: null,
            InputBranchCount: 1,
            Duration: TimeSpan.Zero);

        Assert.Null(evt.GraphName);
        Assert.Null(evt.BranchId);
    }
}
