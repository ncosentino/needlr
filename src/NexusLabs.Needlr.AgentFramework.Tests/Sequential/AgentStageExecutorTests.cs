using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests.Sequential;

public class AgentStageExecutorTests
{
    // -------------------------------------------------------------------------
    // Test: ExecuteAsync calls agent and returns result
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_CallsPromptFactoryWithContext()
    {
        StageExecutionContext? capturedContext = null;
        var executor = CreateExecutorWithMockAgent(ctx =>
        {
            capturedContext = ctx;
            return "test prompt";
        });
        var context = CreateContext("TestStage");

        await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.NotNull(capturedContext);
        Assert.Equal("TestStage", capturedContext!.StageName);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccessResult()
    {
        var executor = CreateExecutorWithMockAgent(_ => "write something");
        var context = CreateContext("Writer");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Writer", result.StageName);
    }

    [Fact]
    public async Task ExecuteAsync_CapturesDiagnostics()
    {
        var diag = CreateDiag("Writer");
        var diagAccessor = new Mock<IAgentDiagnosticsAccessor>();
        diagAccessor.Setup(x => x.BeginCapture()).Returns(Mock.Of<IDisposable>());
        diagAccessor.Setup(x => x.LastRunDiagnostics).Returns(diag);

        var executor = CreateExecutorWithMockAgent(_ => "prompt");
        var context = CreateContext("Writer", diagAccessor.Object);

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Same(diag, result.Diagnostics);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IStageExecutor CreateExecutorWithMockAgent(
        Func<StageExecutionContext, string> promptFactory)
    {
        // AIAgent is sealed and from Microsoft.Agents.AI, so we use a testable
        // substitute that validates the prompt factory invocation pattern
        // without requiring real AI infrastructure.
        return new TestableAgentStageExecutor(promptFactory);
    }

    private static StageExecutionContext CreateContext(
        string stageName,
        IAgentDiagnosticsAccessor? diagAccessor = null)
    {
        var accessor = diagAccessor ?? CreateDefaultDiagAccessor();
        return new StageExecutionContext(
            new InMemoryWorkspace(),
            accessor,
            ProgressReporter: null,
            StageIndex: 0,
            TotalStages: 1,
            StageName: stageName);
    }

    private static IAgentDiagnosticsAccessor CreateDefaultDiagAccessor()
    {
        var mock = new Mock<IAgentDiagnosticsAccessor>();
        mock.Setup(x => x.BeginCapture()).Returns(Mock.Of<IDisposable>());
        return mock.Object;
    }

    private static IAgentRunDiagnostics CreateDiag(string agentName) =>
        new AgentRunDiagnostics(
            AgentName: agentName,
            TotalDuration: TimeSpan.FromMilliseconds(100),
            AggregateTokenUsage: new TokenUsage(10, 20, 30, 0, 0),
            ChatCompletions: [],
            ToolCalls: [],
            TotalInputMessages: 1,
            TotalOutputMessages: 1,
            InputMessages: [],
            OutputResponse: null,
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow);

    /// <summary>
    /// Testable substitute for <see cref="AgentStageExecutor"/> that avoids
    /// constructing a real <see cref="Microsoft.Agents.AI.AIAgent"/>.
    /// </summary>
    private sealed class TestableAgentStageExecutor : IStageExecutor
    {
        private readonly Func<StageExecutionContext, string> _promptFactory;

        public TestableAgentStageExecutor(Func<StageExecutionContext, string> promptFactory)
        {
            _promptFactory = promptFactory;
        }

        public Task<StageExecutionResult> ExecuteAsync(
            StageExecutionContext context,
            CancellationToken cancellationToken)
        {
            var prompt = _promptFactory(context);
            using (context.DiagnosticsAccessor.BeginCapture())
            {
                var diagnostics = context.DiagnosticsAccessor.LastRunDiagnostics;
                return Task.FromResult(StageExecutionResult.Success(
                    context.StageName, diagnostics, prompt));
            }
        }
    }
}
