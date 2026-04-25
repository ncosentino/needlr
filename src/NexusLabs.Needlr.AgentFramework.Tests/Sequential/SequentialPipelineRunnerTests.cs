using Moq;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests.Sequential;

public class SequentialPipelineRunnerTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SequentialPipelineRunner CreateRunner(
        IAgentDiagnosticsAccessor? diagnosticsAccessor = null,
        ITokenBudgetTracker? budgetTracker = null,
        IProgressReporterFactory? progressFactory = null)
    {
        return new SequentialPipelineRunner(
            diagnosticsAccessor ?? CreateDiagnosticsAccessor(),
            budgetTracker ?? new TokenBudgetTracker(),
            progressFactory ?? CreateProgressFactory());
    }

    private static IAgentDiagnosticsAccessor CreateDiagnosticsAccessor()
    {
        var mock = new Mock<IAgentDiagnosticsAccessor>();
        mock.Setup(x => x.BeginCapture()).Returns(Mock.Of<IDisposable>());
        return mock.Object;
    }

    private static IProgressReporterFactory CreateProgressFactory()
    {
        var reporter = new Mock<IProgressReporter>();
        reporter.Setup(x => x.NextSequence()).Returns(0);
        reporter.Setup(x => x.WorkflowId).Returns("test-workflow");

        var factory = new Mock<IProgressReporterFactory>();
        factory.Setup(x => x.Create(It.IsAny<string>())).Returns(reporter.Object);
        return factory.Object;
    }

    private static IProgressReporterFactory CreateCapturingProgressFactory(
        List<IProgressEvent> events)
    {
        var reporter = new Mock<IProgressReporter>();
        long seq = 0;
        reporter.Setup(x => x.NextSequence()).Returns(() => Interlocked.Increment(ref seq));
        reporter.Setup(x => x.WorkflowId).Returns("test-workflow");
        reporter.Setup(x => x.Report(It.IsAny<IProgressEvent>()))
            .Callback<IProgressEvent>(e => events.Add(e));

        var factory = new Mock<IProgressReporterFactory>();
        factory.Setup(x => x.Create(It.IsAny<string>())).Returns(reporter.Object);
        return factory.Object;
    }

    private static IWorkspace CreateWorkspace() => new InMemoryWorkspace();

    private static PipelineStage DelegateStage(
        string name,
        Func<StageExecutionContext, CancellationToken, Task>? action = null,
        StageExecutionPolicy? policy = null) =>
        new(name, new DelegateStageExecutor(action ?? ((_, _) => Task.CompletedTask)), policy);

    private static PipelineStage SuccessStage(string name, StageExecutionPolicy? policy = null) =>
        DelegateStage(name, policy: policy);

    private static PipelineStage ThrowingStage(string name, Exception ex) =>
        new(name, new ThrowingExecutor(ex));

    private static PipelineStage CountingStage(string name, Counter counter) =>
        DelegateStage(name, (_, _) =>
        {
            counter.Increment();
            return Task.CompletedTask;
        });

    // -------------------------------------------------------------------------
    // Test: All stages succeed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_AllStagesSucceed_ReturnsSucceededResult()
    {
        var runner = CreateRunner();
        var stages = new[] { SuccessStage("A"), SuccessStage("B"), SuccessStage("C") };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(3, result.Stages.Count);
    }

    // -------------------------------------------------------------------------
    // Test: Stage throws → aborts and preserves exception
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_StageThrows_AbortsAndPreservesException()
    {
        var runner = CreateRunner();
        var boom = new InvalidOperationException("boom");
        var stages = new[] { SuccessStage("A"), ThrowingStage("B", boom), SuccessStage("C") };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("boom", result.ErrorMessage);
        Assert.Same(boom, result.Exception);
        Assert.Single(result.Stages); // only "A" completed
    }

    // -------------------------------------------------------------------------
    // Test: CancellationToken → returns cancelled, not failed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_CancellationToken_ReturnsCancelledNotFailed()
    {
        var runner = CreateRunner();
        using var cts = new CancellationTokenSource();

        var stages = new[]
        {
            DelegateStage("A", (_, _) =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            }),
            SuccessStage("B"),
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            runner.RunAsync(CreateWorkspace(), stages, options: null, cts.Token));
    }

    // -------------------------------------------------------------------------
    // Test: Budget exhausted → structured budget error
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_BudgetExhausted_ReturnsStructuredBudgetError()
    {
        var budgetTracker = new TokenBudgetTracker();
        var runner = CreateRunner(budgetTracker: budgetTracker);

        var stages = new[]
        {
            DelegateStage("A", (_, ct) =>
            {
                // Simulate budget exceeded by throwing OperationCanceledException
                // with TokenBudgetExceededException inner
                throw new OperationCanceledException(
                    "Budget exceeded",
                    new TokenBudgetExceededException(1000, 500));
            }),
        };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Exception);
        Assert.IsType<TokenBudgetExceededException>(result.Exception);
    }

    // -------------------------------------------------------------------------
    // Test: ShouldSkip → skips stage
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_ShouldSkip_SkipsStageAndRecordsIt()
    {
        var runner = CreateRunner();
        var counter = new Counter();

        var stages = new[]
        {
            SuccessStage("A"),
            CountingStage("B-skip", counter) with
            {
                Policy = new StageExecutionPolicy { ShouldSkip = _ => true },
            },
            SuccessStage("C"),
        };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Stages.Count);
        Assert.Equal(0, counter.Value); // B was skipped
    }

    // -------------------------------------------------------------------------
    // Test: PostValidation fails → retries up to MaxAttempts
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_PostValidationFails_RetriesUpToMaxAttempts()
    {
        var runner = CreateRunner();
        var counter = new Counter();

        var stages = new[]
        {
            new PipelineStage(
                "Validator",
                new CountingExecutor(counter),
                new StageExecutionPolicy
                {
                    MaxAttempts = 3,
                    PostValidation = result =>
                        counter.Value < 3 ? "not yet" : null,
                }),
        };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(3, counter.Value);
    }

    // -------------------------------------------------------------------------
    // Test: PostValidation fails all attempts → throws StageValidationException
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_PostValidationFailsAllAttempts_ThrowsStageValidationException()
    {
        var runner = CreateRunner();

        var stages = new[]
        {
            new PipelineStage(
                "AlwaysFails",
                new DelegateStageExecutor((_, _) => Task.CompletedTask),
                new StageExecutionPolicy
                {
                    MaxAttempts = 2,
                    PostValidation = _ => "always bad",
                }),
        };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Exception);
        Assert.IsType<StageValidationException>(result.Exception);
        var sve = (StageValidationException)result.Exception!;
        Assert.Equal("AlwaysFails", sve.StageName);
        Assert.Equal("always bad", sve.ValidationError);
    }

    // -------------------------------------------------------------------------
    // Test: CompletionGate rejects → failed result
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_CompletionGate_FailsWhenGateRejects()
    {
        var runner = CreateRunner();
        var stages = new[] { SuccessStage("A") };
        var options = new SequentialPipelineOptions
        {
            CompletionGate = _ => "output not acceptable",
        };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("output not acceptable", result.ErrorMessage);
    }

    // -------------------------------------------------------------------------
    // Test: Progress events emitted in order
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_EmitsProgressEvents_InOrder()
    {
        var events = new List<IProgressEvent>();
        var runner = CreateRunner(progressFactory: CreateCapturingProgressFactory(events));
        var stages = new[] { SuccessStage("A"), SuccessStage("B") };

        await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        // Should have: WorkflowStarted, AgentInvoked(A), AgentCompleted(A), AgentInvoked(B), AgentCompleted(B), WorkflowCompleted
        Assert.IsType<WorkflowStartedEvent>(events[0]);
        Assert.IsType<AgentInvokedEvent>(events[1]);
        Assert.IsType<AgentCompletedEvent>(events[2]);
        Assert.IsType<AgentInvokedEvent>(events[3]);
        Assert.IsType<AgentCompletedEvent>(events[4]);
        Assert.IsType<WorkflowCompletedEvent>(events[5]);
    }

    // -------------------------------------------------------------------------
    // Test: Captures diagnostics per stage
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_CapturesDiagnosticsPerStage()
    {
        var diagAccessor = new Mock<IAgentDiagnosticsAccessor>();
        var diag = CreateDiag("A");
        diagAccessor.Setup(x => x.BeginCapture()).Returns(Mock.Of<IDisposable>());
        diagAccessor.Setup(x => x.LastRunDiagnostics).Returns(diag);

        var runner = CreateRunner(diagnosticsAccessor: diagAccessor.Object);

        var executor = new Mock<IStageExecutor>();
        executor.Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StageExecutionContext ctx, CancellationToken _) =>
                StageExecutionResult.Success(ctx.StageName, diag, "response text"));

        var stages = new[] { new PipelineStage("A", executor.Object) };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Single(result.Stages);
        Assert.Same(diag, result.Stages[0].Diagnostics);
    }

    // -------------------------------------------------------------------------
    // Test: Mixed agent and delegate stages both work
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_MixedAgentAndDelegateStages_BothWork()
    {
        var runner = CreateRunner();
        var delegateCalled = false;

        var mockExecutor = new Mock<IStageExecutor>();
        mockExecutor.Setup(x => x.ExecuteAsync(It.IsAny<StageExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StageExecutionContext ctx, CancellationToken _) =>
                StageExecutionResult.Success(ctx.StageName, null, "agent output"));

        var stages = new PipelineStage[]
        {
            new("AgentStage", mockExecutor.Object),
            DelegateStage("DelegateStage", (_, _) =>
            {
                delegateCalled = true;
                return Task.CompletedTask;
            }),
        };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Stages.Count);
        Assert.True(delegateCalled);
        Assert.Equal("agent output", result.Stages[0].FinalResponse?.Text);
    }

    // -------------------------------------------------------------------------
    // Test: Empty stage list → succeeds
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_EmptyStageList_ReturnsSucceeded()
    {
        var runner = CreateRunner();

        var result = await runner.RunAsync(CreateWorkspace(), [], options: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Stages);
    }

    // -------------------------------------------------------------------------
    // Test helpers (co-located per house style)
    // -------------------------------------------------------------------------

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

    private sealed class Counter
    {
        private int _value;
        public int Value => _value;
        public void Increment() => Interlocked.Increment(ref _value);
    }

    private sealed class ThrowingExecutor(Exception exception) : IStageExecutor
    {
        public Task<StageExecutionResult> ExecuteAsync(
            StageExecutionContext context,
            CancellationToken cancellationToken) =>
            throw exception;
    }

    private sealed class CountingExecutor(Counter counter) : IStageExecutor
    {
        public Task<StageExecutionResult> ExecuteAsync(
            StageExecutionContext context,
            CancellationToken cancellationToken)
        {
            counter.Increment();
            return Task.FromResult(StageExecutionResult.Success(
                context.StageName, diagnostics: null, responseText: null));
        }
    }

    // -------------------------------------------------------------------------
    // Test: HTTP timeout → structured error, not crash
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_HttpTimeout_ReturnsFailedResultNotThrow()
    {
        var runner = CreateRunner();

        // Simulate HttpClient.Timeout: TaskCanceledException wrapping TimeoutException
        var timeout = new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
            new TimeoutException("The operation was canceled."));

        var stages = new[] { SuccessStage("A"), ThrowingStage("B", timeout), SuccessStage("C") };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Exception);
        Assert.Single(result.Stages); // only "A" completed
    }

    [Fact]
    public async Task RunAsync_NonCallerCancellation_ReturnsFailedResultNotThrow()
    {
        var runner = CreateRunner();

        // OperationCanceledException without TimeoutException inner — e.g. internal CTS
        var cancel = new OperationCanceledException("Some internal cancellation");
        var stages = new[] { ThrowingStage("A", cancel) };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotNull(result.Exception);
    }
}
