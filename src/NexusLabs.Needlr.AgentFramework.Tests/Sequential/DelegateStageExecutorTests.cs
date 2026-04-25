using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests.Sequential;

public class DelegateStageExecutorTests
{
    // -------------------------------------------------------------------------
    // Test: Delegate is invoked
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_InvokesDelegate()
    {
        var called = false;
        var executor = new DelegateStageExecutor((_, _) =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var context = CreateContext("Transform");

        await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(called);
    }

    // -------------------------------------------------------------------------
    // Test: Returns success with no diagnostics or response
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccessWithNoDiagnosticsOrResponse()
    {
        var executor = new DelegateStageExecutor((_, _) => Task.CompletedTask);
        var context = CreateContext("Transform");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.Diagnostics);
        Assert.Null(result.ResponseText);
        Assert.Equal("Transform", result.StageName);
    }

    // -------------------------------------------------------------------------
    // Test: Passes context and cancellation token through
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_PassesContextAndToken()
    {
        StageExecutionContext? capturedContext = null;
        CancellationToken capturedToken = default;

        var executor = new DelegateStageExecutor((ctx, ct) =>
        {
            capturedContext = ctx;
            capturedToken = ct;
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        var context = CreateContext("Check");

        await executor.ExecuteAsync(context, cts.Token);

        Assert.NotNull(capturedContext);
        Assert.Equal("Check", capturedContext!.StageName);
        Assert.Equal(cts.Token, capturedToken);
    }

    // -------------------------------------------------------------------------
    // Test: Delegate exception propagates
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_DelegateThrows_ExceptionPropagates()
    {
        var executor = new DelegateStageExecutor((_, _) =>
            throw new InvalidOperationException("delegate error"));
        var context = CreateContext("Boom");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(context, CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static StageExecutionContext CreateContext(string stageName)
    {
        var diagAccessor = new Mock<IAgentDiagnosticsAccessor>();
        diagAccessor.Setup(x => x.BeginCapture()).Returns(Mock.Of<IDisposable>());

        return new StageExecutionContext(
            new InMemoryWorkspace(),
            diagAccessor.Object,
            ProgressReporter: null,
            StageIndex: 0,
            TotalStages: 1,
            StageName: stageName);
    }
}
