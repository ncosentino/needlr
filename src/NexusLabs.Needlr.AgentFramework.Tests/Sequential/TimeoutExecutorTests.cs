using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests.Sequential;

public class TimeoutExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_CompletesBeforeTimeout_ReturnsResult()
    {
        var expectedResult = StageExecutionResult.Success("Stage", diagnostics: null, responseText: "done");
        var inner = new Mock<IStageExecutor>();
        inner
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var executor = new TimeoutExecutor(inner.Object, TimeSpan.FromSeconds(10));
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("done", result.ResponseText);
    }

    [Fact]
    public async Task ExecuteAsync_ExceedsTimeout_ThrowsCancellation()
    {
        var inner = new Mock<IStageExecutor>();
        inner
            .Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .Returns<StageExecutionContext, CancellationToken>(async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return StageExecutionResult.Success("Stage", diagnostics: null, responseText: "late");
            });

        var executor = new TimeoutExecutor(inner.Object, TimeSpan.FromMilliseconds(50));
        var context = CreateContext("Stage");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => executor.ExecuteAsync(context, CancellationToken.None));
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
