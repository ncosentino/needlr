using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Generators.Tests;

/// <summary>
/// Tests for <see cref="IEquatable{T}"/> implementations on graph model structs.
/// </summary>
public sealed class GraphModelEqualityTests
{
    // -------------------------------------------------------------------------
    // GraphEdgeEntry
    // -------------------------------------------------------------------------

    [Fact]
    public void GraphEdgeEntry_EqualValues_AreEqual()
    {
        var a = new GraphEdgeEntry("global::MyApp.Source", "Source", "G", "global::MyApp.Target", "cond", true, 1);
        var b = new GraphEdgeEntry("global::MyApp.Source", "Source", "G", "global::MyApp.Target", "cond", true, 1);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GraphEdgeEntry_DifferentCondition_AreNotEqual()
    {
        var a = new GraphEdgeEntry("global::MyApp.Source", "Source", "G", "global::MyApp.Target", "cond1", true, null);
        var b = new GraphEdgeEntry("global::MyApp.Source", "Source", "G", "global::MyApp.Target", "cond2", true, null);

        Assert.NotEqual(a, b);
        Assert.True(a != b);
        Assert.False(a == b);
    }

    [Fact]
    public void GraphEdgeEntry_DifferentIsRequired_AreNotEqual()
    {
        var a = new GraphEdgeEntry("global::MyApp.Source", "Source", "G", "global::MyApp.Target", null, true, null);
        var b = new GraphEdgeEntry("global::MyApp.Source", "Source", "G", "global::MyApp.Target", null, false, null);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GraphEdgeEntry_NullCondition_EqualToNullCondition()
    {
        var a = new GraphEdgeEntry("global::MyApp.Source", "Source", "G", "global::MyApp.Target", null, true, null);
        var b = new GraphEdgeEntry("global::MyApp.Source", "Source", "G", "global::MyApp.Target", null, true, null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GraphEdgeEntry_EqualsObject_WorksCorrectly()
    {
        var a = new GraphEdgeEntry("global::MyApp.Source", "Source", "G", "global::MyApp.Target", null, true, null);
        object b = new GraphEdgeEntry("global::MyApp.Source", "Source", "G", "global::MyApp.Target", null, true, null);

        Assert.True(a.Equals(b));
    }

    [Fact]
    public void GraphEdgeEntry_EqualsNull_ReturnsFalse()
    {
        var a = new GraphEdgeEntry("global::MyApp.Source", "Source", "G", "global::MyApp.Target", null, true, null);

        Assert.False(a.Equals(null));
    }

    // -------------------------------------------------------------------------
    // GraphEntryPointEntry
    // -------------------------------------------------------------------------

    [Fact]
    public void GraphEntryPointEntry_EqualValues_AreEqual()
    {
        var a = new GraphEntryPointEntry("global::MyApp.Agent", "Agent", "G", 0);
        var b = new GraphEntryPointEntry("global::MyApp.Agent", "Agent", "G", 0);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GraphEntryPointEntry_DifferentRoutingMode_AreNotEqual()
    {
        var a = new GraphEntryPointEntry("global::MyApp.Agent", "Agent", "G", 0);
        var b = new GraphEntryPointEntry("global::MyApp.Agent", "Agent", "G", 1);

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void GraphEntryPointEntry_DifferentGraphName_AreNotEqual()
    {
        var a = new GraphEntryPointEntry("global::MyApp.Agent", "Agent", "G1", 0);
        var b = new GraphEntryPointEntry("global::MyApp.Agent", "Agent", "G2", 0);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GraphEntryPointEntry_EqualsObject_WorksCorrectly()
    {
        var a = new GraphEntryPointEntry("global::MyApp.Agent", "Agent", "G", 0);
        object b = new GraphEntryPointEntry("global::MyApp.Agent", "Agent", "G", 0);

        Assert.True(a.Equals(b));
    }

    // -------------------------------------------------------------------------
    // GraphNodeEntry
    // -------------------------------------------------------------------------

    [Fact]
    public void GraphNodeEntry_EqualValues_AreEqual()
    {
        var a = new GraphNodeEntry("global::MyApp.Agent", "Agent", "G", 0);
        var b = new GraphNodeEntry("global::MyApp.Agent", "Agent", "G", 0);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GraphNodeEntry_DifferentJoinMode_AreNotEqual()
    {
        var a = new GraphNodeEntry("global::MyApp.Agent", "Agent", "G", 0);
        var b = new GraphNodeEntry("global::MyApp.Agent", "Agent", "G", 1);

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void GraphNodeEntry_EqualsObject_WorksCorrectly()
    {
        var a = new GraphNodeEntry("global::MyApp.Agent", "Agent", "G", 0);
        object b = new GraphNodeEntry("global::MyApp.Agent", "Agent", "G", 0);

        Assert.True(a.Equals(b));
    }

    [Fact]
    public void GraphNodeEntry_EqualsNull_ReturnsFalse()
    {
        var a = new GraphNodeEntry("global::MyApp.Agent", "Agent", "G", 0);

        Assert.False(a.Equals(null));
    }

    // -------------------------------------------------------------------------
    // GraphReducerEntry
    // -------------------------------------------------------------------------

    [Fact]
    public void GraphReducerEntry_EqualValues_AreEqual()
    {
        var a = new GraphReducerEntry("global::MyApp.Agent", "Agent", "G", "Reduce");
        var b = new GraphReducerEntry("global::MyApp.Agent", "Agent", "G", "Reduce");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GraphReducerEntry_DifferentMethod_AreNotEqual()
    {
        var a = new GraphReducerEntry("global::MyApp.Agent", "Agent", "G", "Reduce");
        var b = new GraphReducerEntry("global::MyApp.Agent", "Agent", "G", "Merge");

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void GraphReducerEntry_EqualsObject_WorksCorrectly()
    {
        var a = new GraphReducerEntry("global::MyApp.Agent", "Agent", "G", "Reduce");
        object b = new GraphReducerEntry("global::MyApp.Agent", "Agent", "G", "Reduce");

        Assert.True(a.Equals(b));
    }

    [Fact]
    public void GraphReducerEntry_EqualsNull_ReturnsFalse()
    {
        var a = new GraphReducerEntry("global::MyApp.Agent", "Agent", "G", "Reduce");

        Assert.False(a.Equals(null));
    }
}
