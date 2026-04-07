namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentResilienceAttributeTests
{
    [Fact]
    public void DefaultValues_MaxRetries2_TimeoutZero()
    {
        var attr = new AgentResilienceAttribute();

        Assert.Equal(2, attr.MaxRetries);
        Assert.Equal(0, attr.TimeoutSeconds);
    }

    [Fact]
    public void CustomValues_ArePreserved()
    {
        var attr = new AgentResilienceAttribute(maxRetries: 5, timeoutSeconds: 300);

        Assert.Equal(5, attr.MaxRetries);
        Assert.Equal(300, attr.TimeoutSeconds);
    }

    [Fact]
    public void ZeroRetries_IsValid()
    {
        var attr = new AgentResilienceAttribute(maxRetries: 0);

        Assert.Equal(0, attr.MaxRetries);
    }

    [Fact]
    public void Attribute_CanBeReadFromType()
    {
        var attr = typeof(ResilientPluginTestAgent)
            .GetCustomAttributes(typeof(AgentResilienceAttribute), false)
            .Cast<AgentResilienceAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attr);
        Assert.Equal(3, attr!.MaxRetries);
        Assert.Equal(60, attr.TimeoutSeconds);
    }
}
