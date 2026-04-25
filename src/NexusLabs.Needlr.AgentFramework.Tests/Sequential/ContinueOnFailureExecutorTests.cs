// Tests intentionally pass CancellationToken.None to verify the distinction between
// caller cancellation and internal/timeout cancellation. This is the behavior under test.
#pragma warning disable xUnit1051

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests.Sequential;

public class ContinueOnFailureExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_InnerSucceeds_ReturnsSuccess()
    {
        var innerResult = StageExecutionResult.Success("Stage", diagnostics: null, responseText: "ok");
        var inner = new Mock<IStageExecutor>();
        inner
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(innerResult);

        var executor = new ContinueOnFailureExecutor(inner.Object);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("ok", result.ResponseText);
    }

    [Fact]
    public async Task ExecuteAsync_InnerThrows_ReturnsFailed()
    {
        var inner = new Mock<IStageExecutor>();
        inner
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var executor = new ContinueOnFailureExecutor(inner.Object);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Exception);
        Assert.Equal("boom", result.Exception!.Message);
        Assert.Equal("Stage", result.StageName);
    }

    [Fact]
    public async Task ExecuteAsync_InnerThrows_CallsOnFailure()
    {
        Exception? captured = null;
        var inner = new Mock<IStageExecutor>();
        inner
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var executor = new ContinueOnFailureExecutor(inner.Object, ex => captured = ex);
        var context = CreateContext("Stage");

        await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("boom", captured!.Message);
    }

    [Fact]
    public async Task ExecuteAsync_InnerThrowsCancellation_WhenCallerCancelled_Rethrows()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var inner = new Mock<IStageExecutor>();
        inner
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var executor = new ContinueOnFailureExecutor(inner.Object);
        var context = CreateContext("Stage", callerToken: cts.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => executor.ExecuteAsync(context, cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_HttpTimeout_ReturnsFailed()
    {
        // HTTP timeouts are OperationCanceledException but caller token is NOT cancelled
        var inner = new Mock<IStageExecutor>();
        inner
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException(
                "Timeout", new TimeoutException("The operation timed out.")));

        Exception? captured = null;
        var executor = new ContinueOnFailureExecutor(inner.Object, ex => captured = ex);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(captured);
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutViaLinkedToken_ReturnsFailed()
    {
        // TimeoutExecutor creates a linked CTS — its token is cancelled but the
        // caller's token is NOT. The decorator should treat this as a failure, not rethrow.
        using var timeoutCts = new CancellationTokenSource();
        timeoutCts.Cancel(); // simulates TimeoutExecutor's linked CTS firing

        var inner = new Mock<IStageExecutor>();
        inner
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(timeoutCts.Token));

        var executor = new ContinueOnFailureExecutor(inner.Object);
        // CallerCancellationToken is NOT cancelled — only the linked one is
        var context = CreateContext("Stage", callerToken: CancellationToken.None);

        var result = await executor.ExecuteAsync(context, timeoutCts.Token);

        Assert.False(result.Succeeded);
    }

    private static StageExecutionContext CreateContext(
        string stageName,
        CancellationToken callerToken = default)
    {
        var diagAccessor = new Mock<IAgentDiagnosticsAccessor>();
        diagAccessor.Setup(x => x.BeginCapture()).Returns(Mock.Of<IDisposable>());

        return new StageExecutionContext(
            new InMemoryWorkspace(),
            diagAccessor.Object,
            ProgressReporter: null,
            StageIndex: 0,
            TotalStages: 1,
            StageName: stageName,
            CallerCancellationToken: callerToken);
    }
}
