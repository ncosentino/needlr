using System.ComponentModel;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Testing;
using NexusLabs.Needlr.AgentFramework.Workspace;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Dogfood tests that use <see cref="AgentScenarioRunner"/> and <see cref="IAgentScenario"/>
/// to test agent behavior — proving the testing package works as intended.
/// </summary>
public class DogfoodScenarioTests
{
    [Fact]
    public async Task WorkspaceWriteScenario_AgentWritesToWorkspace_VerifiesContent()
    {
        var runner = CreateRunner(responseText: "I have saved the note.");

        var result = await runner.RunAsync(
            new WorkspaceWriteScenario(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, $"Scenario failed: {result.ExecutionError?.Message ?? result.VerificationError?.Message}");
    }

    [Fact]
    public async Task GreetingScenario_AgentResponds_VerifiesNonEmpty()
    {
        var runner = CreateRunner(responseText: "Hello! Nice to meet you.");

        var result = await runner.RunAsync(
            new GreetingScenario(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ResponseText);
    }

    [Fact]
    public async Task FailingVerifyScenario_ReportsVerificationError()
    {
        var runner = CreateRunner(responseText: "some response");

        var result = await runner.RunAsync(
            new AlwaysFailVerifyScenario(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.VerificationError);
        Assert.IsType<ScenarioVerificationException>(result.VerificationError);
    }

    // -------------------------------------------------------------------------
    // Scenario implementations — these are the IAgentScenario classes
    // that dogfood the testing package
    // -------------------------------------------------------------------------

    private sealed class WorkspaceWriteScenario : IAgentScenario
    {
        public string Name => "workspace-write";
        public string Description => "Verifies agent can write to workspace via execution context";
        public string SystemPrompt => "You are a helpful agent. Write notes when asked.";
        public string UserPrompt => "Save a note about testing.";

        public void SeedWorkspace(IWorkspace workspace)
        {
            workspace.WriteFile("readme.md", "# Test Workspace");
        }

        public void Verify(IWorkspace workspace, IAgentRunDiagnostics? diagnostics)
        {
            if (!workspace.FileExists("readme.md"))
                throw new ScenarioVerificationException(Name, "readme.md was not preserved");
        }
    }

    private sealed class GreetingScenario : IAgentScenario
    {
        public string Name => "greeting";
        public string Description => "Verifies agent produces a non-empty response";
        public string SystemPrompt => "You are a friendly greeter.";
        public string UserPrompt => "Hello!";

        public void SeedWorkspace(IWorkspace workspace) { }

        public void Verify(IWorkspace workspace, IAgentRunDiagnostics? diagnostics)
        {
            // No workspace assertions — just verifying the run completed
        }
    }

    private sealed class AlwaysFailVerifyScenario : IAgentScenario
    {
        public string Name => "always-fail";
        public string Description => "Always fails verification to test error reporting";
        public string SystemPrompt => "agent";
        public string UserPrompt => "test";

        public void SeedWorkspace(IWorkspace workspace) { }

        public void Verify(IWorkspace workspace, IAgentRunDiagnostics? diagnostics)
        {
            throw new ScenarioVerificationException(Name, "intentional failure for testing");
        }
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static AgentScenarioRunner CreateRunner(string responseText)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, responseText)]));

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object))
            .BuildServiceProvider(config);

        return new AgentScenarioRunner(
            sp.GetRequiredService<IAgentFactory>(),
            sp.GetRequiredService<IAgentExecutionContextAccessor>(),
            sp.GetRequiredService<IAgentDiagnosticsAccessor>());
    }
}
