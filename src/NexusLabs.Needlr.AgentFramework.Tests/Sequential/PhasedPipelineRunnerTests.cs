using Moq;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests.Sequential;

public class PhasedPipelineRunnerTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunPhasedAsync_SinglePhase_ExecutesStagesInOrder()
    {
        var order = new List<string>();
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                DelegateStage("A", (_, _) => { order.Add("A"); return Task.CompletedTask; }),
                DelegateStage("B", (_, _) => { order.Add("B"); return Task.CompletedTask; }),
            ]),
        };
        var runner = CreateRunner();

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.True(result.Succeeded, "Pipeline should succeed");
        Assert.Equal(2, result.Stages.Count);
        Assert.Equal(["A", "B"], order);
    }

    [Fact]
    public async Task RunPhasedAsync_MultiplePhases_ExecutesPhasesInOrder()
    {
        var order = new List<string>();
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                DelegateStage("A", (_, _) => { order.Add("A"); return Task.CompletedTask; }),
            ]),
            new PipelinePhase("P2",
            [
                DelegateStage("B", (_, _) => { order.Add("B"); return Task.CompletedTask; }),
            ]),
            new PipelinePhase("P3",
            [
                DelegateStage("C", (_, _) => { order.Add("C"); return Task.CompletedTask; }),
            ]),
        };
        var runner = CreateRunner();

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.True(result.Succeeded, "Pipeline should succeed");
        Assert.Equal(3, result.Stages.Count);
        Assert.Equal(["A", "B", "C"], order);
    }

    [Fact]
    public async Task RunPhasedAsync_OnEnterAsync_FiresBeforeFirstStage()
    {
        var order = new List<string>();
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                DelegateStage("Stage1", (_, _) => { order.Add("Stage1"); return Task.CompletedTask; }),
            ],
            new PipelinePhasePolicy
            {
                OnEnterAsync = (_, _) => { order.Add("OnEnter"); return ValueTask.CompletedTask; },
            }),
        };
        var runner = CreateRunner();

        await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.Equal(["OnEnter", "Stage1"], order);
    }

    [Fact]
    public async Task RunPhasedAsync_OnExitAsync_FiresAfterLastStage()
    {
        var order = new List<string>();
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                DelegateStage("Stage1", (_, _) => { order.Add("Stage1"); return Task.CompletedTask; }),
            ],
            new PipelinePhasePolicy
            {
                OnExitAsync = (_, _) => { order.Add("OnExit"); return ValueTask.CompletedTask; },
            }),
        };
        var runner = CreateRunner();

        await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.Equal(["Stage1", "OnExit"], order);
    }

    [Fact]
    public async Task RunPhasedAsync_LifecycleOrdering_PhaseStarted_OnEnter_Stages_OnExit_PhaseCompleted()
    {
        var order = new List<string>();
        var events = new List<IProgressEvent>();
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                DelegateStage("Stage1", (_, _) => { order.Add("Stage1"); return Task.CompletedTask; }),
            ],
            new PipelinePhasePolicy
            {
                OnEnterAsync = (_, _) => { order.Add("OnEnter"); return ValueTask.CompletedTask; },
                OnExitAsync = (_, _) => { order.Add("OnExit"); return ValueTask.CompletedTask; },
            }),
        };
        var runner = CreateRunner(progressFactory: CreateCapturingProgressFactory(events));

        await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        var phaseStarted = events.FindIndex(e => e is PhaseStartedEvent);
        var phaseCompleted = events.FindIndex(e => e is PhaseCompletedEvent);
        Assert.True(phaseStarted >= 0, "PhaseStartedEvent should be emitted");
        Assert.True(phaseCompleted >= 0, "PhaseCompletedEvent should be emitted");
        Assert.True(phaseStarted < phaseCompleted, "PhaseStarted should precede PhaseCompleted");
        Assert.Equal(["OnEnter", "Stage1", "OnExit"], order);
    }

    [Fact]
    public async Task RunPhasedAsync_OnEnterAsync_FiresBeforeShouldSkipEvaluation()
    {
        var order = new List<string>();
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                DelegateStage("Stage1", policy: new StageExecutionPolicy
                {
                    ShouldSkip = _ => { order.Add("ShouldSkip"); return true; },
                }),
            ],
            new PipelinePhasePolicy
            {
                OnEnterAsync = (_, _) => { order.Add("OnEnter"); return ValueTask.CompletedTask; },
            }),
        };
        var runner = CreateRunner();

        await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.Equal(["OnEnter", "ShouldSkip"], order);
    }

    [Fact]
    public async Task RunPhasedAsync_OnExitAsync_FiresEvenOnStageFailure()
    {
        var exitCalled = false;
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                new PipelineStage("Failing", new ThrowingExecutor(new InvalidOperationException("boom"))),
            ],
            new PipelinePhasePolicy
            {
                OnExitAsync = (_, _) => { exitCalled = true; return ValueTask.CompletedTask; },
            }),
        };
        var runner = CreateRunner();

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.False(result.Succeeded, "Pipeline should fail");
        Assert.True(exitCalled, "OnExitAsync should fire even on stage failure");
    }

    [Fact]
    public async Task RunPhasedAsync_EmptyPhase_OnEnterAndEventsStillFire()
    {
        var enterCalled = false;
        var exitCalled = false;
        var events = new List<IProgressEvent>();
        var phases = new[]
        {
            new PipelinePhase("Empty", [],
            new PipelinePhasePolicy
            {
                OnEnterAsync = (_, _) => { enterCalled = true; return ValueTask.CompletedTask; },
                OnExitAsync = (_, _) => { exitCalled = true; return ValueTask.CompletedTask; },
            }),
        };
        var runner = CreateRunner(progressFactory: CreateCapturingProgressFactory(events));

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.True(result.Succeeded, "Empty phase should succeed");
        Assert.True(enterCalled, "OnEnterAsync should fire for empty phase");
        Assert.True(exitCalled, "OnExitAsync should fire for empty phase");
        Assert.Contains(events, e => e is PhaseStartedEvent);
        Assert.Contains(events, e => e is PhaseCompletedEvent);
    }

    [Fact]
    public async Task RunPhasedAsync_PhaseContext_HasCorrectMetadata()
    {
        PhaseContext? captured = null;
        var phases = new[]
        {
            new PipelinePhase("Alpha", [SuccessStage("S1")]),
            new PipelinePhase("Beta", [SuccessStage("S2")],
            new PipelinePhasePolicy
            {
                OnEnterAsync = (ctx, _) => { captured = ctx; return ValueTask.CompletedTask; },
            }),
            new PipelinePhase("Gamma", [SuccessStage("S3")]),
        };
        var runner = CreateRunner();

        await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.NotNull(captured);
        Assert.Equal("Beta", captured!.PhaseName);
        Assert.Equal(1, captured.PhaseIndex);
        Assert.Equal(3, captured.TotalPhases);
    }

    [Fact]
    public async Task RunPhasedAsync_StageContext_HasPhaseMetadata()
    {
        StageExecutionContext? captured = null;
        var phases = new[]
        {
            new PipelinePhase("Alpha", [SuccessStage("S1")]),
            new PipelinePhase("Beta",
            [
                SuccessStage("S2a"),
                DelegateStage("S2b", (ctx, _) => { captured = ctx; return Task.CompletedTask; }),
                SuccessStage("S2c"),
            ]),
        };
        var runner = CreateRunner();

        await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.NotNull(captured);
        Assert.Equal("Beta", captured!.PhaseName);
        Assert.Equal(1, captured.PhaseIndex);
        Assert.Equal(1, captured.StageIndexInPhase);
        Assert.Equal(3, captured.TotalStagesInPhase);
        Assert.Equal(2, captured.StageIndex);
    }

    [Fact]
    public async Task RunPhasedAsync_GlobalStageIndex_SpansPhases()
    {
        var indices = new List<int>();
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                DelegateStage("A", (ctx, _) => { indices.Add(ctx.StageIndex); return Task.CompletedTask; }),
                DelegateStage("B", (ctx, _) => { indices.Add(ctx.StageIndex); return Task.CompletedTask; }),
            ]),
            new PipelinePhase("P2",
            [
                DelegateStage("C", (ctx, _) => { indices.Add(ctx.StageIndex); return Task.CompletedTask; }),
            ]),
        };
        var runner = CreateRunner();

        await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.Equal([0, 1, 2], indices);
    }

    [Fact]
    public async Task RunPhasedAsync_PhaseName_PreservedOnStageResults()
    {
        var phases = new[]
        {
            new PipelinePhase("Alpha", [SuccessStage("S1")]),
            new PipelinePhase("Beta", [SuccessStage("S2"), SuccessStage("S3")]),
        };
        var runner = CreateRunner();

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.Equal(3, result.Stages.Count);
        Assert.Equal("Alpha", result.Stages[0].PhaseName);
        Assert.Equal("Beta", result.Stages[1].PhaseName);
        Assert.Equal("Beta", result.Stages[2].PhaseName);
    }

    [Fact]
    public async Task RunPhasedAsync_TypedState_AccessibleInPhaseAndStageContexts()
    {
        object? phaseState = null;
        object? stageState = null;
        var state = new TestState("hello");
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                DelegateStage("S1", (ctx, _) =>
                {
                    stageState = ctx.GetRequiredState<TestState>();
                    return Task.CompletedTask;
                }),
            ],
            new PipelinePhasePolicy
            {
                OnEnterAsync = (ctx, _) =>
                {
                    phaseState = ctx.GetRequiredState<TestState>();
                    return ValueTask.CompletedTask;
                },
            }),
        };
        var runner = CreateRunner();

        await runner.RunPhasedAsync(CreateWorkspace(), phases, state, options: null, _ct);

        Assert.Same(state, phaseState);
        Assert.Same(state, stageState);
    }

    [Fact]
    public async Task RunPhasedAsync_PhaseBudget_ScopesEntirePhase()
    {
        var budgetTracker = new TokenBudgetTracker();
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                SuccessStage("S1"),
                SuccessStage("S2"),
            ],
            new PipelinePhasePolicy { TokenBudget = 100_000 }),
        };
        var runner = CreateRunner(budgetTracker: budgetTracker);

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.True(result.Succeeded, "Pipeline should succeed");
    }

    [Fact]
    public async Task RunPhasedAsync_OnExitAsync_CanReadBudgetStateBeforeDispose()
    {
        var budgetTracker = new TokenBudgetTracker();
        long? maxTokensInExit = null;
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                SuccessStage("S1"),
            ],
            new PipelinePhasePolicy
            {
                TokenBudget = 42_000,
                OnExitAsync = (_, _) =>
                {
                    maxTokensInExit = budgetTracker.MaxTokens;
                    return ValueTask.CompletedTask;
                },
            }),
        };
        var runner = CreateRunner(budgetTracker: budgetTracker);

        await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.Equal(42_000, maxTokensInExit);
    }

    [Fact]
    public async Task RunPhasedAsync_StageBudget_NestsWithinPhaseBudget()
    {
        var budgetTracker = new TokenBudgetTracker();
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                SuccessStage("S1", new StageExecutionPolicy { TokenBudget = 10_000 }),
            ],
            new PipelinePhasePolicy { TokenBudget = 100_000 }),
        };
        var runner = CreateRunner(budgetTracker: budgetTracker);

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.True(result.Succeeded, "Pipeline should succeed with nested budgets");
    }

    [Fact]
    public async Task RunPhasedAsync_TotalStages_CountsAcrossAllPhases()
    {
        int? totalStages = null;
        var phases = new[]
        {
            new PipelinePhase("P1", [SuccessStage("A"), SuccessStage("B")]),
            new PipelinePhase("P2",
            [
                DelegateStage("C", (ctx, _) => { totalStages = ctx.TotalStages; return Task.CompletedTask; }),
            ]),
        };
        var runner = CreateRunner();

        await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.Equal(3, totalStages);
    }

    [Fact]
    public async Task RunPhasedAsync_StageFailure_AbortsPhaseAndPipeline()
    {
        var secondStageRan = false;
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                new PipelineStage("Fail", new ThrowingExecutor(new InvalidOperationException("boom"))),
                DelegateStage("Never", (_, _) => { secondStageRan = true; return Task.CompletedTask; }),
            ]),
        };
        var runner = CreateRunner();

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.False(result.Succeeded, "Pipeline should fail");
        Assert.Contains("boom", result.ErrorMessage);
        Assert.False(secondStageRan, "Second stage should not run after failure");
    }

    [Fact]
    public async Task RunPhasedAsync_OnEnterAsyncFailure_AbortsPipeline()
    {
        var stageRan = false;
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                DelegateStage("S1", (_, _) => { stageRan = true; return Task.CompletedTask; }),
            ],
            new PipelinePhasePolicy
            {
                OnEnterAsync = (_, _) => throw new InvalidOperationException("enter failed"),
            }),
        };
        var runner = CreateRunner();

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.False(result.Succeeded, "Pipeline should fail on OnEnterAsync failure");
        Assert.Contains("enter failed", result.ErrorMessage);
        Assert.False(stageRan, "Stage should not run when OnEnterAsync fails");
    }

    [Fact]
    public async Task RunPhasedAsync_CompletionGate_EvaluatedAfterAllPhases()
    {
        var phases = new[]
        {
            new PipelinePhase("P1", [SuccessStage("S1")]),
        };
        var options = new SequentialPipelineOptions
        {
            CompletionGate = _ => "gate failed",
        };
        var runner = CreateRunner();

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options, _ct);

        Assert.False(result.Succeeded, "Pipeline should fail on gate");
        Assert.Equal("gate failed", result.ErrorMessage);
    }

    [Fact]
    public async Task RunPhasedAsync_PhaseStartedEvent_HasCorrectMetadata()
    {
        var events = new List<IProgressEvent>();
        var phases = new[]
        {
            new PipelinePhase("Analysis", [SuccessStage("S1"), SuccessStage("S2")]),
            new PipelinePhase("Synthesis", [SuccessStage("S3")]),
        };
        var runner = CreateRunner(progressFactory: CreateCapturingProgressFactory(events));

        await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        var phaseEvents = events.OfType<PhaseStartedEvent>().ToList();
        Assert.Equal(2, phaseEvents.Count);
        Assert.Equal("Analysis", phaseEvents[0].PhaseName);
        Assert.Equal(0, phaseEvents[0].PhaseIndex);
        Assert.Equal(2, phaseEvents[0].TotalPhases);
        Assert.Equal(2, phaseEvents[0].StageCount);
        Assert.Equal("Synthesis", phaseEvents[1].PhaseName);
        Assert.Equal(1, phaseEvents[1].PhaseIndex);
        Assert.Equal(1, phaseEvents[1].StageCount);
    }

    [Fact]
    public async Task RunPhasedAsync_PhaseCompletedEvent_HasCorrectMetadata()
    {
        var events = new List<IProgressEvent>();
        var phases = new[]
        {
            new PipelinePhase("P1", [SuccessStage("S1")]),
        };
        var runner = CreateRunner(progressFactory: CreateCapturingProgressFactory(events));

        await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        var completed = events.OfType<PhaseCompletedEvent>().Single();
        Assert.Equal("P1", completed.PhaseName);
        Assert.Equal(0, completed.PhaseIndex);
        Assert.True(completed.Succeeded, "Phase should report success");
        Assert.True(completed.Duration > TimeSpan.Zero, "Duration should be positive");
    }

    [Fact]
    public async Task RunPhasedAsync_SamePhaseNameMultipleTimes_EachGetsOwnOnEnter()
    {
        var enterCount = 0;
        var policy = new PipelinePhasePolicy
        {
            OnEnterAsync = (_, _) => { Interlocked.Increment(ref enterCount); return ValueTask.CompletedTask; },
        };
        var phases = new[]
        {
            new PipelinePhase("Writing", [SuccessStage("S1")], policy),
            new PipelinePhase("Editing", [SuccessStage("S2")]),
            new PipelinePhase("Writing", [SuccessStage("S3")], policy),
        };
        var runner = CreateRunner();

        await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.Equal(2, enterCount);
    }

    [Fact]
    public async Task RunPhasedAsync_OnEnterAsync_CanReconfigureWorkspace()
    {
        string? valueSeenByStage = null;
        var phases = new[]
        {
            new PipelinePhase("Setup", [SuccessStage("Noop")],
            new PipelinePhasePolicy
            {
                OnEnterAsync = (ctx, _) =>
                {
                    ctx.Workspace.TryWriteFile("config.txt", "phase-configured");
                    return ValueTask.CompletedTask;
                },
            }),
            new PipelinePhase("Work",
            [
                DelegateStage("Read", (ctx, _) =>
                {
                    var read = ctx.Workspace.TryReadFile("config.txt");
                    valueSeenByStage = read.Value.Content;
                    return Task.CompletedTask;
                }),
            ]),
        };
        var runner = CreateRunner();

        await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.Equal("phase-configured", valueSeenByStage);
    }

    [Fact]
    public async Task RunPhasedAsync_SkippedStage_PreservesPhaseName()
    {
        var phases = new[]
        {
            new PipelinePhase("P1",
            [
                SuccessStage("S1", new StageExecutionPolicy { ShouldSkip = _ => true }),
            ]),
        };
        var runner = CreateRunner();

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.Single(result.Stages);
        Assert.Equal("P1", result.Stages[0].PhaseName);
        Assert.Equal(StageOutcome.Skipped, result.Stages[0].Outcome);
    }

    [Fact]
    public async Task RunPhasedAsync_PlannedStageCount_ReflectsTotalAcrossPhases()
    {
        var phases = new[]
        {
            new PipelinePhase("P1", [SuccessStage("A"), SuccessStage("B")]),
            new PipelinePhase("P2", [SuccessStage("C")]),
        };
        var runner = CreateRunner();

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        Assert.Equal(3, result.PlannedStageCount);
    }

    [Fact]
    public async Task RunPhasedAsync_PipelineLevelBudget_ScopesEntirePipeline()
    {
        var phases = new[]
        {
            new PipelinePhase("P1", [SuccessStage("S1")]),
            new PipelinePhase("P2", [SuccessStage("S2")]),
        };
        var options = new SequentialPipelineOptions { TotalTokenBudget = 500_000 };
        var runner = CreateRunner();

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options, _ct);

        Assert.True(result.Succeeded, "Pipeline should succeed within budget");
    }

    [Fact]
    public async Task RunAsync_ExistingFlatApi_StillWorksWithoutPhases()
    {
        var stages = new[] { SuccessStage("A"), SuccessStage("B") };
        var runner = CreateRunner();

        var result = await runner.RunAsync(CreateWorkspace(), stages, options: null, _ct);

        Assert.True(result.Succeeded, "Flat pipeline should still work");
        Assert.Equal(2, result.Stages.Count);
        Assert.Null(result.Stages[0].PhaseName);
    }

    [Fact]
    public async Task RunPhasedAsync_PhaseGroupAggregation_CanGroupByPhaseName()
    {
        var phases = new[]
        {
            new PipelinePhase("Alpha", [SuccessStage("S1"), SuccessStage("S2")]),
            new PipelinePhase("Beta", [SuccessStage("S3")]),
        };
        var runner = CreateRunner();

        var result = await runner.RunPhasedAsync(CreateWorkspace(), phases, options: null, _ct);

        var byPhase = result.Stages.GroupBy(s => s.PhaseName).ToDictionary(g => g.Key!, g => g.Count());
        Assert.Equal(2, byPhase["Alpha"]);
        Assert.Equal(1, byPhase["Beta"]);
    }

    private sealed record TestState(string Value);

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
}

internal sealed class ThrowingExecutor(Exception ex) : IStageExecutor
{
    public Task<StageExecutionResult> ExecuteAsync(
        StageExecutionContext context,
        CancellationToken cancellationToken)
    {
        throw ex;
    }
}
