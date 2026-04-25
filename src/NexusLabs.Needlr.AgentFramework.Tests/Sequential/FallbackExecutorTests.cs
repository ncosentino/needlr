using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests.Sequential;

public class FallbackExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_PrimarySucceeds_ReturnsPrimaryResult()
    {
        var primaryResult = StageExecutionResult.Success("Stage", diagnostics: null, responseText: "primary");
        var primary = new Mock<IStageExecutor>();
        primary
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(primaryResult);

        var fallback = new Mock<IStageExecutor>();

        var executor = new FallbackExecutor(primary.Object, fallback.Object);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("primary", result.ResponseText);
        fallback.Verify(
            x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PrimaryFails_ReturnsFallbackResult()
    {
        var primary = new Mock<IStageExecutor>();
        primary
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("primary failed"));

        var fallbackResult = StageExecutionResult.Success("Stage", diagnostics: null, responseText: "fallback");
        var fallback = new Mock<IStageExecutor>();
        fallback
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackResult);

        var executor = new FallbackExecutor(primary.Object, fallback.Object);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("fallback", result.ResponseText);
    }

    [Fact]
    public async Task ExecuteAsync_PrimaryCancelled_WhenCallerCancelled_Rethrows()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var primary = new Mock<IStageExecutor>();
        primary
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var fallback = new Mock<IStageExecutor>();

        var executor = new FallbackExecutor(primary.Object, fallback.Object);
        var context = CreateContext("Stage");

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => executor.ExecuteAsync(context, cts.Token));

        fallback.Verify(
            x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PrimaryHttpTimeout_UsesFallback()
    {
        var primary = new Mock<IStageExecutor>();
        primary
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Timeout", new TimeoutException()));

        var fallbackResult = StageExecutionResult.Success("Stage", diagnostics: null, responseText: "fallback");
        var fallback = new Mock<IStageExecutor>();
        fallback
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackResult);

        var executor = new FallbackExecutor(primary.Object, fallback.Object);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("fallback", result.ResponseText);
    }

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
