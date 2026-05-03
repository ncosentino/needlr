using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests.Sequential;

public class IterativeLoopStageExecutorTests
{
    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExecuteAsync_SucceededLoop_ReturnsSuccessWithDiagnosticsAndResponseText()
    {
        var diag = CreateDiag("Writer");
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.NaturalCompletion,
            diagnostics: diag,
            responseText: "Draft complete.");
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Writer");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.True(result.Succeeded, "Expected success for a succeeded loop");
        Assert.Equal("Writer", result.StageName);
        Assert.Same(diag, result.Diagnostics);
        Assert.Equal("Draft complete.", result.ResponseText);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_SucceededLoop_NullDiagnostics_ReturnsSuccessWithNullDiagnostics()
    {
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed,
            nullDiagnostics: true);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage", diagReturnsNull: true);

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.True(result.Succeeded, "Expected success for a succeeded loop");
        Assert.Null(result.Diagnostics);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_SucceededLoop_NullFinalResponse_ReturnsNullResponseText()
    {
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed,
            responseText: null);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.True(result.Succeeded, "Expected success");
        Assert.Null(result.ResponseText);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_SucceededLoop_EmptyResponseText_ReturnsEmptyString()
    {
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed,
            responseText: "");
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.True(result.Succeeded, "Expected success");
        Assert.Equal("", result.ResponseText);
        _mocks.VerifyAll();
    }

    [Theory]
    [InlineData(TerminationReason.MaxIterationsReached)]
    [InlineData(TerminationReason.MaxToolCallsReached)]
    [InlineData(TerminationReason.StallDetected)]
    [InlineData(TerminationReason.BudgetPressure)]
    [InlineData(TerminationReason.Error)]
    [InlineData(TerminationReason.Cancelled)]
    public async Task ExecuteAsync_FailedLoop_ReturnsFailedResult(TerminationReason termination)
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: termination,
            errorMessage: "something went wrong",
            iterationCount: 3);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.False(result.Succeeded, "Expected failure for non-success loop");
        Assert.Equal("Stage", result.StageName);
        Assert.NotNull(result.Exception);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_FailedLoop_ExceptionMessageIncludesTerminationReason()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.MaxIterationsReached,
            errorMessage: "hit the limit",
            iterationCount: 5);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Writer");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.Contains("MaxIterationsReached", result.Exception!.Message);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_FailedLoop_ExceptionMessageIncludesIterationCount()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.StallDetected,
            iterationCount: 7);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Reviewer");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.Contains("7", result.Exception!.Message);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_FailedLoop_ExceptionMessageIncludesLoopErrorMessage()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.Error,
            errorMessage: "prompt factory blew up");
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.Contains("prompt factory blew up", result.Exception!.Message);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTreatAsSuccess_OverridesNonSuccessToSuccess()
    {
        var diag = CreateDiag("Writer");
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.MaxIterationsReached,
            diagnostics: diag,
            responseText: "partial output");
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            shouldTreatAsSuccess: r => r.Termination == TerminationReason.MaxIterationsReached);
        var context = CreateContext("Writer");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.True(result.Succeeded, "Expected shouldTreatAsSuccess to override failure");
        Assert.Same(diag, result.Diagnostics);
        Assert.Equal("partial output", result.ResponseText);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTreatAsSuccess_ReturnsFalse_RemainsFailure()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.MaxIterationsReached);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            shouldTreatAsSuccess: _ => false);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.False(result.Succeeded, "Expected failure when predicate returns false");
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTreatAsSuccess_Null_DefaultsToFailure()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.MaxIterationsReached);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            shouldTreatAsSuccess: null);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.False(result.Succeeded, "Expected failure when predicate is null");
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTreatAsSuccess_NotCalledWhenLoopSucceeded()
    {
        var called = false;
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            shouldTreatAsSuccess: _ =>
            {
                called = true;
                return false;
            });
        var context = CreateContext("Stage");

        await executor.ExecuteAsync(context, _ct);

        Assert.False(called, "shouldTreatAsSuccess should not be called when loop succeeded");
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTreatAsSuccess_ReceivesActualLoopResult()
    {
        IterativeLoopResult? captured = null;
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.MaxToolCallsReached,
            iterationCount: 4);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            shouldTreatAsSuccess: r =>
            {
                captured = r;
                return true;
            });
        var context = CreateContext("Stage");

        await executor.ExecuteAsync(context, _ct);

        Assert.NotNull(captured);
        Assert.Same(loopResult, captured);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_DefaultFailureDisposition_IsAbortPipeline()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.StallDetected);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.Equal(FailureDisposition.AbortPipeline, result.FailureDisposition);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_ContinueAdvisoryDisposition_FlowsToResult()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.MaxIterationsReached);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            failureDisposition: FailureDisposition.ContinueAdvisory);
        var context = CreateContext("Advisory");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.False(result.Succeeded, "Expected failure");
        Assert.Equal(FailureDisposition.ContinueAdvisory, result.FailureDisposition);
        _mocks.VerifyAll();
    }

    [Theory]
    [InlineData(TerminationReason.MaxIterationsReached)]
    [InlineData(TerminationReason.MaxToolCallsReached)]
    [InlineData(TerminationReason.StallDetected)]
    [InlineData(TerminationReason.Error)]
    [InlineData(TerminationReason.Cancelled)]
    [InlineData(TerminationReason.BudgetPressure)]
    public async Task ExecuteAsync_FailureDisposition_AppliedForAllTerminationReasons(
        TerminationReason termination)
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: termination);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            failureDisposition: FailureDisposition.ContinueAdvisory);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.Equal(FailureDisposition.ContinueAdvisory, result.FailureDisposition);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_DispositionNotUsedWhenShouldTreatAsSuccessOverrides()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.MaxIterationsReached);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            shouldTreatAsSuccess: _ => true,
            failureDisposition: FailureDisposition.AbortPipeline);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.True(result.Succeeded, "shouldTreatAsSuccess should override to success");
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_DefaultContextFactory_UsesContextWorkspace()
    {
        var workspace = new InMemoryWorkspace();
        IWorkspace? capturedWorkspace = null;
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed);
        var loop = _mocks.Create<IIterativeAgentLoop>();
        loop
            .Setup(x => x.RunAsync(
                It.IsAny<IterativeLoopOptions>(),
                It.IsAny<IterativeContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<IterativeLoopOptions, IterativeContext, CancellationToken>(
                (_, ctx, _) => capturedWorkspace = ctx.Workspace)
            .ReturnsAsync(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage", workspace: workspace);

        await executor.ExecuteAsync(context, _ct);

        Assert.Same(workspace, capturedWorkspace);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_CustomContextFactory_IsInvoked()
    {
        var customWorkspace = new InMemoryWorkspace();
        IWorkspace? capturedWorkspace = null;
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed);
        var loop = _mocks.Create<IIterativeAgentLoop>();
        loop
            .Setup(x => x.RunAsync(
                It.IsAny<IterativeLoopOptions>(),
                It.IsAny<IterativeContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<IterativeLoopOptions, IterativeContext, CancellationToken>(
                (_, ctx, _) => capturedWorkspace = ctx.Workspace)
            .ReturnsAsync(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            contextFactory: _ => new IterativeContext { Workspace = customWorkspace });
        var context = CreateContext("Stage");

        await executor.ExecuteAsync(context, _ct);

        Assert.Same(customWorkspace, capturedWorkspace);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_CustomContextFactory_ReceivesStageExecutionContext()
    {
        StageExecutionContext? capturedContext = null;
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            contextFactory: ctx =>
            {
                capturedContext = ctx;
                return new IterativeContext { Workspace = ctx.Workspace };
            });
        var context = CreateContext("MyStage");

        await executor.ExecuteAsync(context, _ct);

        Assert.NotNull(capturedContext);
        Assert.Equal("MyStage", capturedContext!.StageName);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_CustomContextFactory_CanPrePopulateState()
    {
        IDictionary<string, object>? capturedState = null;
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed);
        var loop = _mocks.Create<IIterativeAgentLoop>();
        loop
            .Setup(x => x.RunAsync(
                It.IsAny<IterativeLoopOptions>(),
                It.IsAny<IterativeContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<IterativeLoopOptions, IterativeContext, CancellationToken>(
                (_, ctx, _) => capturedState = ctx.State)
            .ReturnsAsync(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            contextFactory: ctx =>
            {
                var iterCtx = new IterativeContext { Workspace = ctx.Workspace };
                iterCtx.State["seeded"] = true;
                return iterCtx;
            });
        var context = CreateContext("Stage");

        await executor.ExecuteAsync(context, _ct);

        Assert.NotNull(capturedState);
        Assert.True(
            capturedState!.ContainsKey("seeded"),
            "Expected pre-populated state to be visible to loop");
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_OptionsFactory_ReceivesStageExecutionContext()
    {
        StageExecutionContext? capturedContext = null;
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            ctx =>
            {
                capturedContext = ctx;
                return CreateDefaultOptions();
            });
        var context = CreateContext("TheStage");

        await executor.ExecuteAsync(context, _ct);

        Assert.NotNull(capturedContext);
        Assert.Equal("TheStage", capturedContext!.StageName);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_OptionsFactory_ReturnedOptionsPassedToLoop()
    {
        IterativeLoopOptions? capturedOptions = null;
        var expectedOptions = CreateDefaultOptions();
        expectedOptions.LoopName = "custom-loop";
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed);
        var loop = _mocks.Create<IIterativeAgentLoop>();
        loop
            .Setup(x => x.RunAsync(
                It.IsAny<IterativeLoopOptions>(),
                It.IsAny<IterativeContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<IterativeLoopOptions, IterativeContext, CancellationToken>(
                (opts, _, _) => capturedOptions = opts)
            .ReturnsAsync(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, _ => expectedOptions);
        var context = CreateContext("Stage");

        await executor.ExecuteAsync(context, _ct);

        Assert.Same(expectedOptions, capturedOptions);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_DiagnosticsFromLoopResultPreferred()
    {
        var loopDiag = CreateDiag("LoopDiag");
        var accessorDiag = CreateDiag("AccessorDiag");
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed,
            diagnostics: loopDiag);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.Same(loopDiag, result.Diagnostics);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToAccessorDiagnosticsWhenLoopDiagNull()
    {
        var accessorDiag = CreateDiag("AccessorDiag");
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed,
            nullDiagnostics: true);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage", accessorDiagnostics: accessorDiag);

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.Same(accessorDiag, result.Diagnostics);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_BeginCaptureIsCalled()
    {
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed);
        var loop = SetupLoop(loopResult);
        var diagAccessor = _mocks.Create<IAgentDiagnosticsAccessor>();
        var captureScope = _mocks.Create<IDisposable>();
        diagAccessor.Setup(x => x.BeginCapture()).Returns(captureScope.Object);
        captureScope.Setup(x => x.Dispose());
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage", diagAccessor: diagAccessor.Object);

        await executor.ExecuteAsync(context, _ct);

        diagAccessor.Verify(x => x.BeginCapture(), Times.Once);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_CaptureScopeDisposedAfterExecution()
    {
        var disposed = false;
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed);
        var loop = SetupLoop(loopResult);
        var diagAccessor = _mocks.Create<IAgentDiagnosticsAccessor>();
        var captureScope = _mocks.Create<IDisposable>();
        diagAccessor.Setup(x => x.BeginCapture()).Returns(captureScope.Object);
        captureScope.Setup(x => x.Dispose()).Callback(() => disposed = true);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage", diagAccessor: diagAccessor.Object);

        await executor.ExecuteAsync(context, _ct);

        Assert.True(disposed, "BeginCapture scope should be disposed after execution");
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_BothDiagnosticsNull_ReturnsNullDiagnostics()
    {
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed,
            nullDiagnostics: true);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage", diagReturnsNull: true);

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.Null(result.Diagnostics);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_OnLoopCompleted_CalledWithLoopResultAndContext()
    {
        IterativeLoopResult? capturedResult = null;
        StageExecutionContext? capturedContext = null;
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            onLoopCompleted: (r, ctx) =>
            {
                capturedResult = r;
                capturedContext = ctx;
            });
        var context = CreateContext("Writer");

        await executor.ExecuteAsync(context, _ct);

        Assert.Same(loopResult, capturedResult);
        Assert.Same(context, capturedContext);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_OnLoopCompleted_CalledOnFailurePath()
    {
        var called = false;
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.Error,
            errorMessage: "boom");
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            onLoopCompleted: (_, _) => called = true);
        var context = CreateContext("Stage");

        await executor.ExecuteAsync(context, _ct);

        Assert.True(called, "onLoopCompleted should be called on failure path");
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_OnLoopCompleted_Null_NoError()
    {
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            onLoopCompleted: null);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.True(result.Succeeded, "Expected success with null callback");
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_OnLoopCompleted_CalledBeforeResultMapping()
    {
        bool? wasSucceeded = null;
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.MaxIterationsReached);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            onLoopCompleted: (r, _) => wasSucceeded = r.Succeeded,
            shouldTreatAsSuccess: _ => true);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.True(result.Succeeded, "shouldTreatAsSuccess should override");
        Assert.False(wasSucceeded, "onLoopCompleted should see raw Succeeded=false before override");
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_OnLoopCompleted_NotCalledWhenLoopThrows()
    {
        var called = false;
        var loop = _mocks.Create<IIterativeAgentLoop>();
        loop
            .Setup(x => x.RunAsync(
                It.IsAny<IterativeLoopOptions>(),
                It.IsAny<IterativeContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("loop exploded"));
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            onLoopCompleted: (_, _) => called = true);
        var context = CreateContext("Stage");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(context, _ct));

        Assert.False(called, "onLoopCompleted should not be called when loop throws");
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_OnLoopCompleted_CanCaptureTerminationReason()
    {
        string? terminationReason = null;
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.StallDetected);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            onLoopCompleted: (r, _) => terminationReason = r.Termination.ToString(),
            shouldTreatAsSuccess: _ => true);
        var context = CreateContext("Stage");

        await executor.ExecuteAsync(context, _ct);

        Assert.Equal("StallDetected", terminationReason);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_OnLoopCompleted_CanCaptureIterationCount()
    {
        int? iterationCount = null;
        var loopResult = CreateLoopResult(
            succeeded: true,
            termination: TerminationReason.Completed,
            iterationCount: 7);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            onLoopCompleted: (r, _) => iterationCount = r.Iterations.Count);
        var context = CreateContext("Stage");

        await executor.ExecuteAsync(context, _ct);

        Assert.Equal(7, iterationCount);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_WrappedInTimeoutExecutor_FailedResultReturnedNotException()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.Cancelled);
        var loop = SetupLoop(loopResult);
        var inner = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var executor = new TimeoutExecutor(inner, TimeSpan.FromMinutes(5));
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.False(result.Succeeded, "Expected failed result from cancelled loop");
        Assert.Equal(FailureDisposition.AbortPipeline, result.FailureDisposition);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_WrappedInContinueOnFailureExecutor_FailedResultPassesThrough()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.MaxIterationsReached);
        var loop = SetupLoop(loopResult);
        var inner = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var executor = new ContinueOnFailureExecutor(inner);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.False(result.Succeeded, "Expected failed result to pass through");
        Assert.Equal(FailureDisposition.AbortPipeline, result.FailureDisposition);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_ContinueAdvisory_WrappedInContinueOnFailure_FlowsCorrectly()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.StallDetected);
        var loop = SetupLoop(loopResult);
        var inner = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            failureDisposition: FailureDisposition.ContinueAdvisory);
        var executor = new ContinueOnFailureExecutor(inner);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.False(result.Succeeded, "Expected failure");
        Assert.Equal(FailureDisposition.ContinueAdvisory, result.FailureDisposition);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_WrappedInFallbackExecutor_FailedResultPassesThroughPrimary()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.Error);
        var loop = SetupLoop(loopResult);
        var inner = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var fallbackExecutor = _mocks.Create<IStageExecutor>();
        var executor = new FallbackExecutor(inner, fallbackExecutor.Object);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.False(result.Succeeded, "Expected failed result from primary (no exception to trigger fallback)");
        fallbackExecutor.Verify(
            x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_OptionsFactoryThrows_ExceptionPropagates()
    {
        var loop = _mocks.Create<IIterativeAgentLoop>();
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            _ => throw new InvalidOperationException("factory error"));
        var context = CreateContext("Stage");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(context, _ct));

        Assert.Equal("factory error", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ContextFactoryThrows_ExceptionPropagates()
    {
        var loop = _mocks.Create<IIterativeAgentLoop>();
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            contextFactory: _ => throw new ArgumentException("bad context"));
        var context = CreateContext("Stage");

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => executor.ExecuteAsync(context, _ct));

        Assert.Equal("bad context", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_LoopThrowsUnexpectedException_ExceptionPropagates()
    {
        var loop = _mocks.Create<IIterativeAgentLoop>();
        loop
            .Setup(x => x.RunAsync(
                It.IsAny<IterativeLoopOptions>(),
                It.IsAny<IterativeContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unexpected loop error"));
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(context, _ct));

        Assert.Equal("unexpected loop error", ex.Message);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_CaptureScopeDisposedEvenWhenLoopThrows()
    {
        var disposed = false;
        var loop = _mocks.Create<IIterativeAgentLoop>();
        loop
            .Setup(x => x.RunAsync(
                It.IsAny<IterativeLoopOptions>(),
                It.IsAny<IterativeContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var diagAccessor = _mocks.Create<IAgentDiagnosticsAccessor>();
        var captureScope = _mocks.Create<IDisposable>();
        diagAccessor.Setup(x => x.BeginCapture()).Returns(captureScope.Object);
        captureScope.Setup(x => x.Dispose()).Callback(() => disposed = true);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage", diagAccessor: diagAccessor.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync(context, _ct));

        Assert.True(disposed, "BeginCapture scope should be disposed even on exception");
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_FailedWithAbortPipeline_RunnerSemantics()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.Error);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.False(result.Succeeded, "Expected failure");
        Assert.Equal(FailureDisposition.AbortPipeline, result.FailureDisposition);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_FailedWithContinueAdvisory_RunnerSemantics()
    {
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.StallDetected);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(
            loop.Object,
            DefaultOptionsFactory,
            failureDisposition: FailureDisposition.ContinueAdvisory);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.False(result.Succeeded, "Expected failure");
        Assert.Equal(FailureDisposition.ContinueAdvisory, result.FailureDisposition);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_FailedLoop_DiagnosticsStillCaptured()
    {
        var diag = CreateDiag("FailedStage");
        var loopResult = CreateLoopResult(
            succeeded: false,
            termination: TerminationReason.Error,
            diagnostics: diag);
        var loop = SetupLoop(loopResult);
        var executor = new IterativeLoopStageExecutor(loop.Object, DefaultOptionsFactory);
        var context = CreateContext("Stage");

        var result = await executor.ExecuteAsync(context, _ct);

        Assert.False(result.Succeeded, "Expected failure");
        Assert.Same(diag, result.Diagnostics);
        _mocks.VerifyAll();
    }

    private static Func<StageExecutionContext, IterativeLoopOptions> DefaultOptionsFactory =>
        _ => CreateDefaultOptions();

    private static IterativeLoopOptions CreateDefaultOptions() => new()
    {
        Instructions = "test instructions",
        Tools = [],
        PromptFactory = _ => "test prompt",
        LoopName = "test-loop",
    };

    private Mock<IIterativeAgentLoop> SetupLoop(IterativeLoopResult loopResult)
    {
        var loop = _mocks.Create<IIterativeAgentLoop>();
        loop
            .Setup(x => x.RunAsync(
                It.IsAny<IterativeLoopOptions>(),
                It.IsAny<IterativeContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(loopResult);
        return loop;
    }

    private StageExecutionContext CreateContext(
        string stageName,
        IAgentDiagnosticsAccessor? diagAccessor = null,
        IAgentRunDiagnostics? accessorDiagnostics = null,
        bool diagReturnsNull = false,
        IWorkspace? workspace = null)
    {
        var accessor = diagAccessor;
        if (accessor is null)
        {
            var mock = _mocks.Create<IAgentDiagnosticsAccessor>();
            mock.Setup(x => x.BeginCapture()).Returns(Mock.Of<IDisposable>());

            if (accessorDiagnostics is not null || diagReturnsNull)
            {
                mock.Setup(x => x.LastRunDiagnostics).Returns(
                    diagReturnsNull ? null : accessorDiagnostics);
            }

            accessor = mock.Object;
        }

        return new StageExecutionContext(
            workspace ?? new InMemoryWorkspace(),
            accessor,
            ProgressReporter: null,
            StageIndex: 0,
            TotalStages: 1,
            StageName: stageName);
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

    private static IterativeLoopResult CreateLoopResult(
        bool succeeded,
        TerminationReason termination,
        IAgentRunDiagnostics? diagnostics = null,
        string? responseText = null,
        string? errorMessage = null,
        int iterationCount = 1,
        bool nullDiagnostics = false)
    {
        var iterations = Enumerable.Range(0, iterationCount)
            .Select(i => new IterationRecord(
                Iteration: i,
                ToolCalls: [],
                FinalResponse: null,
                Tokens: new TokenUsage(0, 0, 0, 0, 0),
                Duration: TimeSpan.Zero,
                LlmCallCount: 0,
                ToolCallCount: 0))
            .ToList();

        ChatResponse? finalResponse = responseText is not null
            ? new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))
            : null;

        var config = new IterativeLoopConfiguration(
            ToolResultMode: ToolResultMode.SingleCall,
            MaxIterations: 10,
            MaxToolRoundsPerIteration: 5,
            MaxTotalToolCalls: null,
            BudgetPressureThreshold: null,
            LoopName: "test-loop",
            CheckCompletionAfterToolCalls: ToolCompletionCheckMode.None);

        var effectiveDiag = nullDiagnostics ? null : (diagnostics ?? CreateDiag("test"));

        return new IterativeLoopResult(
            Iterations: iterations,
            FinalResponse: finalResponse,
            Diagnostics: effectiveDiag,
            Succeeded: succeeded,
            ErrorMessage: errorMessage,
            Termination: termination,
            Configuration: config);
    }
}
