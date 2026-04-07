using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Workflows.Middleware;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class ToolResultFunctionMiddlewareTests
{
    [Fact]
    public void ToolResultFunctionMiddleware_Implements_IAIAgentBuilderPlugin()
    {
        var middleware = new ToolResultFunctionMiddleware();

        Assert.IsAssignableFrom<IAIAgentBuilderPlugin>(middleware);
    }

    [Fact]
    public void Configure_WithNullOptions_ThrowsArgumentNull()
    {
        var middleware = new ToolResultFunctionMiddleware();

        Assert.Throws<ArgumentNullException>(() =>
            middleware.Configure(null!));
    }

    [Fact]
    public void Configure_WithValidBuilder_DoesNotThrow()
    {
        // Verify that Configure doesn't throw when given a real builder.
        // We can't easily verify the middleware stack without running an agent,
        // but we can confirm it wires without error.
        var middleware = new ToolResultFunctionMiddleware();

        // AIAgentBuilder requires a base agent — create a minimal one
        var chatClient = new Mock<IChatClient>();
        var baseAgent = chatClient.Object.AsAIAgent(
            name: "test",
            instructions: "test");

        var builder = new AIAgentBuilder(baseAgent);
        var options = new AIAgentBuilderPluginOptions { AgentBuilder = builder };

        middleware.Configure(options);

        // If we reach here, Configure wired successfully
    }
}
