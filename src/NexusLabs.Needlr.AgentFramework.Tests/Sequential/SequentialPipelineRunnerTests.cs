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
        Assert.Equal(3, result.PlannedStageCount);
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
        Assert.Equal(2, result.Stages.Count); // "A" completed + "B" recorded as failed
        Assert.Equal(3, result.PlannedStageCount); // all 3 stages were planned
        Assert.Equal("A", result.Stages[0].AgentName);
        Assert.Equal("B", result.Stages[1].AgentName);
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
    // Part A: AfterExecution hook tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_AfterExecution_CalledForSuccessfulStage()
    {
        var runner = CreateRunner();
        bool called = false;
        var policy = new StageExecutionPolicy
        {
            AfterExecution = (result, ctx) => { called = true; return Task.CompletedTask; },
        };
        var stages = new[] { DelegateStage("A", policy: policy) };

        await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);
        Assert.True(called);
    }

    [Fact]
    public async Task RunAsync_AfterExecution_CalledForFailedStage()
    {
        var runner = CreateRunner();
        bool called = false;
        StageExecutionResult? capturedResult = null;
        var policy = new StageExecutionPolicy
        {
            AfterExecution = (result, ctx) =>
            {
                called = true;
                capturedResult = result;
                return Task.CompletedTask;
            },
        };

        var boom = new InvalidOperationException("boom");
        var executor = new FailingExecutorWithPolicy(boom);
        var stages = new[] { new PipelineStage("A", executor, policy) };

        await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.True(called);
        Assert.NotNull(capturedResult);
        Assert.False(capturedResult!.Succeeded);
    }

    [Fact]
    public async Task RunAsync_AfterExecution_NotCalledForSkippedStage()
    {
        var runner = CreateRunner();
        bool called = false;
        var policy = new StageExecutionPolicy
        {
            ShouldSkip = _ => true,
            AfterExecution = (result, ctx) => { called = true; return Task.CompletedTask; },
        };
        var stages = new[] { DelegateStage("A", policy: policy) };

        await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);
        Assert.False(called);
    }

    [Fact]
    public async Task RunAsync_AfterExecution_CanReadWorkspace()
    {
        var runner = CreateRunner();
        var workspace = CreateWorkspace();
        workspace.TryWriteFile("test.txt", "hello");

        string? readContent = null;
        var policy = new StageExecutionPolicy
        {
            AfterExecution = (result, ctx) =>
            {
                var wsResult = ctx.Workspace.TryReadFile("test.txt");
                readContent = wsResult.Value?.Content;
                return Task.CompletedTask;
            },
        };
        var stages = new[] { DelegateStage("A", policy: policy) };

        await runner.RunAsync(workspace, stages, options: null, CancellationToken.None);
        Assert.Equal("hello", readContent);
    }

    // -------------------------------------------------------------------------
    // Part B: Typed Pipeline State tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithState_StageCanReadState()
    {
        var runner = CreateRunner();
        var state = new TestPipelineState { Counter = 0 };
        var stages = new[]
        {
            DelegateStage("Increment", (ctx, ct) =>
            {
                var s = ctx.GetRequiredState<TestPipelineState>();
                s.Counter++;
                return Task.CompletedTask;
            }),
        };

        await runner.RunAsync(CreateWorkspace(), stages, state, options: null, CancellationToken.None);
        Assert.Equal(1, state.Counter);
    }

    [Fact]
    public async Task RunAsync_WithState_StateSharedAcrossStages()
    {
        var runner = CreateRunner();
        var state = new TestPipelineState { Counter = 0 };
        var stages = new[]
        {
            DelegateStage("First", (ctx, ct) => { ctx.GetRequiredState<TestPipelineState>().Counter++; return Task.CompletedTask; }),
            DelegateStage("Second", (ctx, ct) => { ctx.GetRequiredState<TestPipelineState>().Counter++; return Task.CompletedTask; }),
        };

        await runner.RunAsync(CreateWorkspace(), stages, state, options: null, CancellationToken.None);
        Assert.Equal(2, state.Counter);
    }

    [Fact]
    public async Task RunAsync_WithoutState_GetRequiredStateThrows()
    {
        var runner = CreateRunner();
        var stages = new[]
        {
            DelegateStage("A", (ctx, ct) =>
            {
                Assert.Throws<InvalidOperationException>(() => ctx.GetRequiredState<TestPipelineState>());
                return Task.CompletedTask;
            }),
        };

        await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);
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

    private sealed class FailingExecutorWithPolicy(Exception exception) : IStageExecutor
    {
        public Task<StageExecutionResult> ExecuteAsync(
            StageExecutionContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StageExecutionResult.Failed(context.StageName, exception));
    }

    private sealed class TestPipelineState
    {
        public int Counter { get; set; }
        public string? LastStageName { get; set; }
        public List<string> Findings { get; } = [];
    }

    private static PipelineStage FailedStage(
        string name,
        FailureDisposition disposition) =>
        new(name, new FailedResultExecutor(disposition));

    private sealed class FailedResultExecutor(FailureDisposition disposition) : IStageExecutor
    {
        public Task<StageExecutionResult> ExecuteAsync(
            StageExecutionContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(StageExecutionResult.Failed(
                context.StageName,
                new InvalidOperationException("stage failed"),
                disposition: disposition));
    }

    // -------------------------------------------------------------------------
    // Part C: Failure Disposition tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_StageReturnsFailed_AbortDisposition_AbortsPipeline()
    {
        var runner = CreateRunner();
        var counter = new Counter();
        var stages = new[]
        {
            SuccessStage("A"),
            FailedStage("B", FailureDisposition.AbortPipeline),
            CountingStage("C", counter),
        };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Stages.Count); // A + B (C never ran)
        Assert.Equal(0, counter.Value);
    }

    [Fact]
    public async Task RunAsync_StageReturnsFailed_AdvisoryDisposition_ContinuesPipeline()
    {
        var runner = CreateRunner();
        var counter = new Counter();
        var stages = new[]
        {
            SuccessStage("A"),
            FailedStage("B", FailureDisposition.ContinueAdvisory),
            CountingStage("C", counter),
        };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Stages.Count);
        Assert.Equal(1, counter.Value);
    }

    [Fact]
    public async Task RunAsync_AdvisoryFailure_StageRecordedWithFailedOutcome()
    {
        var runner = CreateRunner();
        var stages = new[]
        {
            SuccessStage("A"),
            FailedStage("B", FailureDisposition.ContinueAdvisory),
        };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.Equal(StageOutcome.Failed, result.Stages[1].Outcome);
    }

    [Fact]
    public async Task RunAsync_SkippedStage_StageRecordedWithSkippedOutcome()
    {
        var runner = CreateRunner();
        var stages = new[]
        {
            SuccessStage("A") with
            {
                Policy = new StageExecutionPolicy { ShouldSkip = _ => true },
            },
        };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.Equal(StageOutcome.Skipped, result.Stages[0].Outcome);
    }

    [Fact]
    public async Task RunAsync_SuccessfulStage_StageRecordedWithSucceededOutcome()
    {
        var runner = CreateRunner();
        var stages = new[] { SuccessStage("A") };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.Equal(StageOutcome.Succeeded, result.Stages[0].Outcome);
    }

    [Fact]
    public async Task RunAsync_MixedAdvisoryAndSuccess_PipelineSucceeds()
    {
        var runner = CreateRunner();
        var stages = new[]
        {
            SuccessStage("A"),
            FailedStage("B", FailureDisposition.ContinueAdvisory),
            SuccessStage("C"),
        };

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Stages.Count);
        Assert.Equal(StageOutcome.Succeeded, result.Stages[0].Outcome);
        Assert.Equal(StageOutcome.Failed, result.Stages[1].Outcome);
        Assert.Equal(StageOutcome.Succeeded, result.Stages[2].Outcome);
    }

    [Fact]
    public async Task RunAsync_AbortFailure_EmitsAgentFailedEvent()
    {
        var events = new List<IProgressEvent>();
        var runner = CreateRunner(progressFactory: CreateCapturingProgressFactory(events));
        var stages = new[]
        {
            FailedStage("A", FailureDisposition.AbortPipeline),
        };

        await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.Contains(events, e => e is AgentFailedEvent f && f.AgentName == "A");
        Assert.DoesNotContain(events, e => e is AgentCompletedEvent c && c.AgentName == "A");
    }

    [Fact]
    public async Task RunAsync_AdvisoryFailure_EmitsAgentFailedEvent()
    {
        var events = new List<IProgressEvent>();
        var runner = CreateRunner(progressFactory: CreateCapturingProgressFactory(events));
        var stages = new[]
        {
            FailedStage("A", FailureDisposition.ContinueAdvisory),
        };

        await runner.RunAsync(CreateWorkspace(), stages, options: null, CancellationToken.None);

        Assert.Contains(events, e => e is AgentFailedEvent f && f.AgentName == "A");
        Assert.DoesNotContain(events, e => e is AgentCompletedEvent c && c.AgentName == "A");
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
        Assert.Equal(2, result.Stages.Count); // "A" completed + "B" recorded as failed
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
