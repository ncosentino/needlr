using System.ComponentModel;
using System.Reflection;

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

public class AgentScenarioRunnerTests
{
    // -------------------------------------------------------------------------
    // Constructor validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_NullAgentFactory_Throws()
    {
        var accessor = new AgentExecutionContextAccessor();
        var diagAccessor = new AgentDiagnosticsAccessor();

        Assert.Throws<ArgumentNullException>(() =>
            new AgentScenarioRunner(null!, accessor, diagAccessor));
    }

    [Fact]
    public void Constructor_NullContextAccessor_Throws()
    {
        var factory = CreateAgentFactory();
        var diagAccessor = new AgentDiagnosticsAccessor();

        Assert.Throws<ArgumentNullException>(() =>
            new AgentScenarioRunner(factory, null!, diagAccessor));
    }

    [Fact]
    public void Constructor_NullDiagnosticsAccessor_Throws()
    {
        var factory = CreateAgentFactory();
        var accessor = new AgentExecutionContextAccessor();

        Assert.Throws<ArgumentNullException>(() =>
            new AgentScenarioRunner(factory, accessor, null!));
    }

    // -------------------------------------------------------------------------
    // RunAsync — null scenario
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_NullScenario_ThrowsArgumentNull()
    {
        var runner = CreateRunner();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            runner.RunAsync(null!, TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // IAgentScenario — interface shape
    // -------------------------------------------------------------------------

    [Fact]
    public void IAgentScenario_HasExpectedMembers()
    {
        var members = typeof(IAgentScenario).GetMembers(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(members, m => m.Name == "Name");
        Assert.Contains(members, m => m.Name == "Description");
        Assert.Contains(members, m => m.Name == "SystemPrompt");
        Assert.Contains(members, m => m.Name == "UserPrompt");
        Assert.Contains(members, m => m.Name == "SeedWorkspace");
        Assert.Contains(members, m => m.Name == "Verify");
    }

    // -------------------------------------------------------------------------
    // ScenarioVerificationException
    // -------------------------------------------------------------------------

    [Fact]
    public void ScenarioVerificationException_ContainsScenarioName()
    {
        var ex = new ScenarioVerificationException("test-scenario", "files missing");

        Assert.Equal("test-scenario", ex.ScenarioName);
        Assert.Contains("test-scenario", ex.Message);
        Assert.Contains("files missing", ex.Message);
    }

    // -------------------------------------------------------------------------
    // ScenarioRunResult record
    // -------------------------------------------------------------------------

    [Fact]
    public void ScenarioRunResult_PreservesAllFields()
    {
        var ws = new InMemoryWorkspace();
        var result = new ScenarioRunResult(
            ScenarioName: "test",
            Workspace: ws,
            Diagnostics: null,
            ResponseText: "hello",
            ExecutionError: null,
            VerificationError: null,
            Succeeded: true);

        Assert.Equal("test", result.ScenarioName);
        Assert.Same(ws, result.Workspace);
        Assert.Equal("hello", result.ResponseText);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ScenarioRunResult_Failed_HasErrors()
    {
        var execError = new Exception("exec failed");
        var verifyError = new ScenarioVerificationException("s", "verify failed");

        var result = new ScenarioRunResult(
            ScenarioName: "fail-test",
            Workspace: new InMemoryWorkspace(),
            Diagnostics: null,
            ResponseText: null,
            ExecutionError: execError,
            VerificationError: verifyError,
            Succeeded: false);

        Assert.False(result.Succeeded);
        Assert.Same(execError, result.ExecutionError);
        Assert.Same(verifyError, result.VerificationError);
    }

    // -------------------------------------------------------------------------
    // C2: RunAsync integration — full lifecycle
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithMockScenario_SeedsWorkspaceAndExecutes()
    {
        var (runner, _) = CreateRunnerWithMockChat(
            responseText: "The answer is 42.");

        var scenario = new Mock<IAgentScenario>();
        scenario.Setup(s => s.Name).Returns("test-scenario");
        scenario.Setup(s => s.Description).Returns("A test");
        scenario.Setup(s => s.SystemPrompt).Returns("You are a test agent.");
        scenario.Setup(s => s.UserPrompt).Returns("What is the answer?");
        scenario.Setup(s => s.SeedWorkspace(It.IsAny<IWorkspace>()))
            .Callback<IWorkspace>(ws => ws.TryWriteFile("seed.txt", "seeded"));
        scenario.Setup(s => s.Verify(It.IsAny<IWorkspace>(), It.IsAny<IAgentRunDiagnostics?>()));

        var result = await runner.RunAsync(scenario.Object, TestContext.Current.CancellationToken);

        // Workspace was seeded
        Assert.True(result.Workspace.FileExists("seed.txt"));
        // SeedWorkspace was called
        scenario.Verify(s => s.SeedWorkspace(It.IsAny<IWorkspace>()), Times.Once);
        // Verify was called
        scenario.Verify(s => s.Verify(It.IsAny<IWorkspace>(), It.IsAny<IAgentRunDiagnostics?>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenVerifyThrows_ResultContainsVerificationError()
    {
        var (runner, _) = CreateRunnerWithMockChat(responseText: "ok");

        var scenario = new Mock<IAgentScenario>();
        scenario.Setup(s => s.Name).Returns("fail-verify");
        scenario.Setup(s => s.SystemPrompt).Returns("agent");
        scenario.Setup(s => s.UserPrompt).Returns("test");
        scenario.Setup(s => s.SeedWorkspace(It.IsAny<IWorkspace>()));
        scenario.Setup(s => s.Verify(It.IsAny<IWorkspace>(), It.IsAny<IAgentRunDiagnostics?>()))
            .Throws(new ScenarioVerificationException("fail-verify", "expected file missing"));

        var result = await runner.RunAsync(scenario.Object, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.VerificationError);
        Assert.IsType<ScenarioVerificationException>(result.VerificationError);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IAgentFactory CreateAgentFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();
    }

    private static AgentScenarioRunner CreateRunner()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .BuildServiceProvider(config);

        return new AgentScenarioRunner(
            sp.GetRequiredService<IAgentFactory>(),
            sp.GetRequiredService<IAgentExecutionContextAccessor>(),
            sp.GetRequiredService<IAgentDiagnosticsAccessor>());
    }

    private static (AgentScenarioRunner Runner, Mock<IChatClient> MockChat) CreateRunnerWithMockChat(
        string responseText = "mock response")
    {
        var config = new ConfigurationBuilder().Build();
        var mockChat = new Mock<IChatClient>();

        // Set up mock to return a canned response
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

        var runner = new AgentScenarioRunner(
            sp.GetRequiredService<IAgentFactory>(),
            sp.GetRequiredService<IAgentExecutionContextAccessor>(),
            sp.GetRequiredService<IAgentDiagnosticsAccessor>());

        return (runner, mockChat);
    }
}
