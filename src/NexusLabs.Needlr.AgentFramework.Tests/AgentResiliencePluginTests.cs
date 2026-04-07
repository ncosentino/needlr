using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Workflows.Middleware;

using Polly;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentResiliencePluginTests
{
    [Fact]
    public void Constructor_WithNullPipeline_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AgentResiliencePlugin(null!));
    }

    [Fact]
    public void Implements_IAIAgentBuilderPlugin()
    {
        var pipeline = new ResiliencePipelineBuilder<AgentResponse>().Build();
        var plugin = new AgentResiliencePlugin(pipeline);

        Assert.IsAssignableFrom<IAIAgentBuilderPlugin>(plugin);
    }

    [Fact]
    public void Configure_WithNullOptions_ThrowsArgumentNull()
    {
        var pipeline = new ResiliencePipelineBuilder<AgentResponse>().Build();
        var plugin = new AgentResiliencePlugin(pipeline);

        Assert.Throws<ArgumentNullException>(() =>
            plugin.Configure(null!));
    }

    [Fact]
    public void Configure_WithValidBuilder_DoesNotThrow()
    {
        var pipeline = new ResiliencePipelineBuilder<AgentResponse>().Build();
        var plugin = new AgentResiliencePlugin(pipeline);

        var chatClient = new Mock<IChatClient>();
        var baseAgent = chatClient.Object.AsAIAgent(name: "test", instructions: "test");
        var builder = new AIAgentBuilder(baseAgent);
        var options = new AIAgentBuilderPluginOptions { AgentBuilder = builder };

        plugin.Configure(options);

        // Verify build doesn't throw (middleware was wired)
        var agent = builder.Build(null);
        Assert.NotNull(agent);
    }
}
