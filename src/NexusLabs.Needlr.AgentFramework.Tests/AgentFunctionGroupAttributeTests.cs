using System.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentFunctionGroupAttributeTests
{
    [Fact]
    public void Constructor_SetsGroupName()
    {
        var attr = new AgentFunctionGroupAttribute("my-group");

        Assert.Equal("my-group", attr.GroupName);
    }

    [Fact]
    public void Constructor_ThrowsWhenGroupNameIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new AgentFunctionGroupAttribute(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsWhenGroupNameIsNullOrWhitespace(string groupName)
    {
        Assert.Throws<ArgumentException>(() => new AgentFunctionGroupAttribute(groupName));
    }

    [Fact]
    public void AttributeUsage_AllowsMultiple()
    {
        var usage = typeof(AgentFunctionGroupAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>()!;

        Assert.True(usage.AllowMultiple);
    }

    [Fact]
    public void AttributeUsage_TargetsClassesOnly()
    {
        var usage = typeof(AgentFunctionGroupAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>()!;

        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
    }

    [Fact]
    public void AttributeUsage_NotInherited()
    {
        var usage = typeof(AgentFunctionGroupAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>()!;

        Assert.False(usage.Inherited);
    }

    [Fact]
    public void MultipleGroupsOnSameClass_BothGroupNamesRetrievable()
    {
        var groupNames = typeof(MultiGroupFixture)
            .GetCustomAttributes<AgentFunctionGroupAttribute>(inherit: false)
            .Select(a => a.GroupName)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(["alpha", "beta"], groupNames);
    }

    [AgentFunctionGroup("alpha")]
    [AgentFunctionGroup("beta")]
    private sealed class MultiGroupFixture { }
}
