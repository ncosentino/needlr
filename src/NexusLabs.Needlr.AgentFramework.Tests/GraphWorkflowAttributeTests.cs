using NexusLabs.Needlr.AgentFramework;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for DAG graph workflow attributes.
/// </summary>
public sealed class GraphWorkflowAttributeTests
{
    [Fact]
    public void AgentGraphEdgeAttribute_StoresGraphNameAndTarget()
    {
        var attr = new AgentGraphEdgeAttribute("Pipeline", typeof(string));

        Assert.Equal("Pipeline", attr.GraphName);
        Assert.Equal(typeof(string), attr.TargetAgentType);
        Assert.Null(attr.Condition);
        Assert.True(attr.IsRequired, "IsRequired should default to true");
    }

    [Fact]
    public void AgentGraphEdgeAttribute_WithConditionAndOptional()
    {
        var attr = new AgentGraphEdgeAttribute("Pipeline", typeof(int))
        {
            Condition = "NeedsData",
            IsRequired = false,
        };

        Assert.Equal("NeedsData", attr.Condition);
        Assert.False(attr.IsRequired);
        Assert.Null(attr.NodeRoutingMode);
    }

    [Fact]
    public void AgentGraphEdgeAttribute_NodeRoutingModeOverride()
    {
        var attr = new AgentGraphEdgeAttribute("Pipeline", typeof(int))
        {
            NodeRoutingMode = GraphRoutingMode.ExclusiveChoice,
        };

        Assert.Equal(GraphRoutingMode.ExclusiveChoice, attr.NodeRoutingMode);
    }

    [Fact]
    public void AgentGraphEdgeAttribute_NodeRoutingModeDefaultsToNull()
    {
        var attr = new AgentGraphEdgeAttribute("Pipeline", typeof(int));

        Assert.Null(attr.NodeRoutingMode);
    }

    [Fact]
    public void AgentGraphEdgeAttribute_NullGraphName_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new AgentGraphEdgeAttribute(null!, typeof(string)));
    }

    [Fact]
    public void AgentGraphEdgeAttribute_NullTargetType_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new AgentGraphEdgeAttribute("Pipeline", null!));
    }

    [Fact]
    public void AgentGraphEntryAttribute_StoresDefaults()
    {
        var attr = new AgentGraphEntryAttribute("Research");

        Assert.Equal("Research", attr.GraphName);
        Assert.Equal(20, attr.MaxSupersteps);
        Assert.Equal(GraphRoutingMode.Deterministic, attr.RoutingMode);
    }

    [Fact]
    public void AgentGraphEntryAttribute_CustomValues()
    {
        var attr = new AgentGraphEntryAttribute("Research")
        {
            MaxSupersteps = 10,
            RoutingMode = GraphRoutingMode.LlmChoice,
        };

        Assert.Equal(10, attr.MaxSupersteps);
        Assert.Equal(GraphRoutingMode.LlmChoice, attr.RoutingMode);
    }

    [Fact]
    public void AgentGraphEntryAttribute_NullGraphName_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new AgentGraphEntryAttribute(null!));
    }

    [Fact]
    public void AgentGraphNodeAttribute_StoresDefaults()
    {
        var attr = new AgentGraphNodeAttribute("Research");

        Assert.Equal("Research", attr.GraphName);
        Assert.Equal(GraphJoinMode.WaitAll, attr.JoinMode);
    }

    [Fact]
    public void AgentGraphNodeAttribute_WaitAny()
    {
        var attr = new AgentGraphNodeAttribute("Research")
        {
            JoinMode = GraphJoinMode.WaitAny,
        };

        Assert.Equal(GraphJoinMode.WaitAny, attr.JoinMode);
    }

    [Fact]
    public void AgentGraphNodeAttribute_NullGraphName_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new AgentGraphNodeAttribute(null!));
    }

    [Fact]
    public void GraphRoutingMode_HasExpectedValues()
    {
        Assert.Equal(0, (int)GraphRoutingMode.Deterministic);
        Assert.Equal(1, (int)GraphRoutingMode.AllMatching);
        Assert.Equal(2, (int)GraphRoutingMode.FirstMatching);
        Assert.Equal(3, (int)GraphRoutingMode.ExclusiveChoice);
        Assert.Equal(4, (int)GraphRoutingMode.LlmChoice);
    }

    [Fact]
    public void GraphJoinMode_HasExpectedValues()
    {
        Assert.Equal(0, (int)GraphJoinMode.WaitAll);
        Assert.Equal(1, (int)GraphJoinMode.WaitAny);
    }
}
