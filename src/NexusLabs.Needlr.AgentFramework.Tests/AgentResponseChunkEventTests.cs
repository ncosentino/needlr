using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed class AgentResponseChunkEventTests
{
    [Fact]
    public void Constructor_PreservesAllProperties()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var evt = new AgentResponseChunkEvent(
            Timestamp: timestamp,
            WorkflowId: "wf-1",
            AgentId: "agent-1",
            ParentAgentId: null,
            Depth: 1,
            SequenceNumber: 42,
            AgentName: "AnalyzerAgent_abc123",
            Text: "Here are the key trends");

        Assert.Equal(timestamp, evt.Timestamp);
        Assert.Equal("wf-1", evt.WorkflowId);
        Assert.Equal("agent-1", evt.AgentId);
        Assert.Null(evt.ParentAgentId);
        Assert.Equal(1, evt.Depth);
        Assert.Equal(42, evt.SequenceNumber);
        Assert.Equal("AnalyzerAgent_abc123", evt.AgentName);
        Assert.Equal("Here are the key trends", evt.Text);
    }

    [Fact]
    public void Implements_IProgressEvent()
    {
        var evt = new AgentResponseChunkEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: "wf-1",
            AgentId: null,
            ParentAgentId: null,
            Depth: 0,
            SequenceNumber: 1,
            AgentName: "TestAgent",
            Text: "chunk");

        Assert.IsAssignableFrom<IProgressEvent>(evt);
    }

    [Fact]
    public void EmptyText_IsAccepted()
    {
        var evt = new AgentResponseChunkEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: "wf-1",
            AgentId: null,
            ParentAgentId: null,
            Depth: 0,
            SequenceNumber: 1,
            AgentName: "TestAgent",
            Text: "");

        Assert.Equal("", evt.Text);
    }

    [Fact]
    public void LargeText_IsPreserved()
    {
        var largeText = new string('x', 10_000);

        var evt = new AgentResponseChunkEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: "wf-1",
            AgentId: null,
            ParentAgentId: null,
            Depth: 0,
            SequenceNumber: 1,
            AgentName: "TestAgent",
            Text: largeText);

        Assert.Equal(10_000, evt.Text.Length);
    }
}
