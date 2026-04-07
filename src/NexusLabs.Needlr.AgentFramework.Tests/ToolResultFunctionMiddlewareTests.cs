using System.ComponentModel;
using System.Reflection;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Tools;
using NexusLabs.Needlr.AgentFramework.Workflows.Middleware;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

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
        var middleware = new ToolResultFunctionMiddleware();

        var chatClient = new Mock<IChatClient>();
        var baseAgent = chatClient.Object.AsAIAgent(
            name: "test",
            instructions: "test");

        var builder = new AIAgentBuilder(baseAgent);
        var options = new AIAgentBuilderPluginOptions { AgentBuilder = builder };

        middleware.Configure(options);
    }

    // -------------------------------------------------------------------------
    // M3: Behavioral test — middleware doesn't break normal agent execution
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Agent_WithToolResultMiddleware_CompletesWithMockChat()
    {
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]));

        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object)
                .AddAgentFunctionsFromAssemblies([Assembly.GetExecutingAssembly()])
                .UsingToolResultMiddleware())
            .BuildServiceProvider(new ConfigurationBuilder().Build())
            .GetRequiredService<IAgentFactory>();

        // Create an agent that has ToolResult-returning functions registered
        var agent = factory.CreateAgent(opts =>
        {
            opts.Name = "ToolResultAgent";
            opts.FunctionTypes = [typeof(ToolResultTestFunctions)];
        });

        // Run the agent — mock returns plain text (no tool calls).
        // The middleware is a pass-through for non-IToolResult returns,
        // so this verifies it wires without breaking the pipeline.
        var response = await agent.RunAsync(
            "hello",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        mockChat.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Agent_WithToolResultMiddleware_AndResilience_CompletesWithMockChat()
    {
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]));

        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object)
                .AddAgentFunctionsFromAssemblies([Assembly.GetExecutingAssembly()])
                .UsingToolResultMiddleware()
                .UsingResilience())
            .BuildServiceProvider(new ConfigurationBuilder().Build())
            .GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent(opts =>
        {
            opts.Name = "FullStackAgent";
            opts.FunctionTypes = [typeof(ToolResultTestFunctions)];
        });

        var response = await agent.RunAsync(
            "hello",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(response);
    }
}

// ---------------------------------------------------------------------------
// Test function class with ToolResult return types
// ---------------------------------------------------------------------------

[AgentFunctionGroup("tool-result-test")]
public sealed class ToolResultTestFunctions
{
    [AgentFunction]
    [Description("Returns a greeting wrapped in ToolResult.")]
    public ToolResult<string, ToolError> GetGreeting() =>
        ToolResult.Ok("Hello from ToolResult!");

    [AgentFunction]
    [Description("Returns a failure wrapped in ToolResult.")]
    public ToolResult<string, ToolError> GetFailure() =>
        ToolResult.Fail<string>("Something went wrong", suggestion: "Try again");
}
