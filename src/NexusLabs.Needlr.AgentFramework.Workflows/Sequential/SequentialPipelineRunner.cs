using System.Diagnostics;

using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Executes a linear sequence of <see cref="PipelineStage"/> instances,
/// evaluating policies (skip, retry, budget) and producing an
/// <see cref="IPipelineRunResult"/> with per-stage diagnostics.
/// </summary>
/// <remarks>
/// <para>
/// This runner is a peer of <see cref="GraphWorkflowRunner"/> for linear pipelines.
/// It supports hybrid agent/programmatic stages via <see cref="IStageExecutor"/>,
/// conditional skipping, post-validation with retries, per-stage and overall token
/// budgets, and structured progress reporting.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var runner = new SequentialPipelineRunner(diagnosticsAccessor, budgetTracker, progressFactory);
/// var stages = new[]
/// {
///     new PipelineStage("Writer", new AgentStageExecutor(writerAgent, ctx =&gt; "Write a draft.")),
///     new PipelineStage("Editor", new AgentStageExecutor(editorAgent, ctx =&gt; "Edit the draft.")),
/// };
/// var result = await runner.RunAsync(workspace, stages, options: null, cancellationToken);
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed class SequentialPipelineRunner
{
    private readonly IAgentDiagnosticsAccessor _diagnosticsAccessor;
    private readonly ITokenBudgetTracker _budgetTracker;
    private readonly IProgressReporterFactory _progressReporterFactory;

    /// <summary>
    /// Initializes a new <see cref="SequentialPipelineRunner"/>.
    /// </summary>
    /// <param name="diagnosticsAccessor">Accessor for capturing per-stage agent diagnostics.</param>
    /// <param name="budgetTracker">Token budget tracker for scoping per-stage and pipeline-level budgets.</param>
    /// <param name="progressReporterFactory">Factory for creating progress reporters.</param>
    public SequentialPipelineRunner(
        IAgentDiagnosticsAccessor diagnosticsAccessor,
        ITokenBudgetTracker budgetTracker,
        IProgressReporterFactory progressReporterFactory)
    {
        _diagnosticsAccessor = diagnosticsAccessor;
        _budgetTracker = budgetTracker;
        _progressReporterFactory = progressReporterFactory;
    }

    /// <summary>
    /// Runs all pipeline stages sequentially, applying policies and collecting results.
    /// </summary>
    /// <param name="workspace">The shared workspace for file I/O across stages.</param>
    /// <param name="stages">The ordered list of stages to execute.</param>
    /// <param name="options">Optional pipeline-level configuration.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>An <see cref="IPipelineRunResult"/> describing the pipeline outcome.</returns>
    public Task<IPipelineRunResult> RunAsync(
        IWorkspace workspace,
        IReadOnlyList<PipelineStage> stages,
        SequentialPipelineOptions? options,
        CancellationToken cancellationToken) =>
        RunCoreAsync(workspace, stages, pipelineState: null, options, cancellationToken);

    /// <summary>
    /// Runs all pipeline stages sequentially with a shared typed state object,
    /// applying policies and collecting results.
    /// </summary>
    /// <typeparam name="TState">The type of the shared pipeline state.</typeparam>
    /// <param name="workspace">The shared workspace for file I/O across stages.</param>
    /// <param name="stages">The ordered list of stages to execute.</param>
    /// <param name="state">A shared state object accessible to all stages via
    /// <see cref="StageExecutionContext.GetRequiredState{T}"/>.</param>
    /// <param name="options">Optional pipeline-level configuration.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>An <see cref="IPipelineRunResult"/> describing the pipeline outcome.</returns>
    public Task<IPipelineRunResult> RunAsync<TState>(
        IWorkspace workspace,
        IReadOnlyList<PipelineStage> stages,
        TState state,
        SequentialPipelineOptions? options,
        CancellationToken cancellationToken) where TState : class =>
        RunCoreAsync(workspace, stages, state, options, cancellationToken);

    private async Task<IPipelineRunResult> RunCoreAsync(
        IWorkspace workspace,
        IReadOnlyList<PipelineStage> stages,
        object? pipelineState,
        SequentialPipelineOptions? options,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var reporter = _progressReporterFactory.Create(Guid.NewGuid().ToString("N"));
        var stageResults = new List<IAgentStageResult>();

        reporter.Report(new WorkflowStartedEvent(
            DateTimeOffset.UtcNow,
            reporter.WorkflowId,
            reporter.AgentId,
            ParentAgentId: null,
            reporter.Depth,
            reporter.NextSequence()));

        IDisposable? pipelineBudgetScope = null;
        try
        {
            if (options?.TotalTokenBudget is { } totalBudget)
            {
                pipelineBudgetScope = _budgetTracker.BeginScope(totalBudget);
            }

            for (var i = 0; i < stages.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stage = stages[i];
                var policy = stage.Policy;

                var context = new StageExecutionContext(
                    workspace,
                    _diagnosticsAccessor,
                    reporter,
                    StageIndex: i,
                    TotalStages: stages.Count,
                    StageName: stage.Name,
                    CallerCancellationToken: cancellationToken,
                    PipelineState: pipelineState);

                // Evaluate ShouldSkip
                if (policy?.ShouldSkip?.Invoke(context) == true)
                {
                    stageResults.Add(new AgentStageResult(
                        stage.Name,
                        FinalResponse: null,
                        Diagnostics: null,
                        Outcome: StageOutcome.Skipped));
                    continue;
                }

                reporter.Report(new AgentInvokedEvent(
                    DateTimeOffset.UtcNow,
                    reporter.WorkflowId,
                    stage.Name,
                    ParentAgentId: null,
                    reporter.Depth,
                    reporter.NextSequence(),
                    stage.Name));

                var maxAttempts = policy?.MaxAttempts ?? 1;
                StageExecutionResult? stageResult = null;
                string? validationError = null;

                IDisposable? stageBudgetScope = null;
                try
                {
                    if (policy?.TokenBudget is { } stageBudget)
                    {
                        stageBudgetScope = _budgetTracker.BeginChildScope(stage.Name, stageBudget);
                    }

                    for (var attempt = 0; attempt < maxAttempts; attempt++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        stageResult = await stage.Executor.ExecuteAsync(context, cancellationToken);

                        if (policy?.PostValidation is { } validate)
                        {
                            validationError = validate(stageResult);
                            if (validationError is null)
                            {
                                break;
                            }

                            // Last attempt failed — will throw after loop
                            if (attempt < maxAttempts - 1)
                            {
                                validationError = null;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Always record the failed stage so it appears in diagnostics.
                    // Capture any partial diagnostics the stage may have produced.
                    var partialDiag = _diagnosticsAccessor.LastRunDiagnostics;
                    stageResults.Add(new AgentStageResult(
                        stage.Name,
                        FinalResponse: null,
                        Diagnostics: partialDiag,
                        Outcome: StageOutcome.Failed));

                    reporter.Report(new AgentFailedEvent(
                        DateTimeOffset.UtcNow,
                        reporter.WorkflowId,
                        stage.Name,
                        ParentAgentId: null,
                        reporter.Depth,
                        reporter.NextSequence(),
                        AgentName: stage.Name,
                        ErrorMessage: ex.Message));

                    throw;
                }
                finally
                {
                    stageBudgetScope?.Dispose();
                }

                if (stageResult is not null && policy?.AfterExecution is { } afterExec)
                {
                    await afterExec(stageResult, context);
                }

                if (validationError is not null)
                {
                    throw new StageValidationException(stage.Name, validationError);
                }

                // Handle explicit failure results from the stage executor.
                if (!stageResult!.Succeeded)
                {
                    stageResults.Add(new AgentStageResult(
                        stage.Name,
                        FinalResponse: null,
                        Diagnostics: stageResult.Diagnostics,
                        Outcome: StageOutcome.Failed));

                    reporter.Report(new AgentFailedEvent(
                        DateTimeOffset.UtcNow,
                        reporter.WorkflowId,
                        stage.Name,
                        ParentAgentId: null,
                        reporter.Depth,
                        reporter.NextSequence(),
                        AgentName: stage.Name,
                        ErrorMessage: stageResult.Exception?.Message ?? "Stage failed"));

                    if (stageResult.FailureDisposition == FailureDisposition.AbortPipeline)
                    {
                        stopwatch.Stop();
                        var errorMsg = stageResult.Exception?.Message
                            ?? $"Stage '{stage.Name}' failed";
                        ReportCompleted(reporter, stopwatch.Elapsed, succeeded: false, errorMsg);
                        return new PipelineRunResult(
                            stageResults,
                            stopwatch.Elapsed,
                            succeeded: false,
                            errorMessage: errorMsg,
                            exception: stageResult.Exception,
                            plannedStageCount: stages.Count);
                    }

                    // ContinueAdvisory — proceed to the next stage.
                    continue;
                }

                ChatResponse? chatResponse = stageResult!.ResponseText is not null
                    ? new ChatResponse(new ChatMessage(ChatRole.Assistant, stageResult.ResponseText))
                    : null;

                stageResults.Add(new AgentStageResult(
                    stage.Name,
                    chatResponse,
                    stageResult.Diagnostics));

                reporter.Report(new AgentCompletedEvent(
                    DateTimeOffset.UtcNow,
                    reporter.WorkflowId,
                    stage.Name,
                    ParentAgentId: null,
                    reporter.Depth,
                    reporter.NextSequence(),
                    stage.Name,
                    Duration: stopwatch.Elapsed,
                    TotalTokens: stageResult.Diagnostics?.AggregateTokenUsage.TotalTokens ?? 0));
            }

            stopwatch.Stop();

            var pipelineResult = new PipelineRunResult(
                stageResults,
                stopwatch.Elapsed,
                succeeded: true,
                errorMessage: null,
                plannedStageCount: stages.Count);

            if (options?.CompletionGate is { } gate)
            {
                var gateError = gate(pipelineResult);
                if (gateError is not null)
                {
                    var failedResult = new PipelineRunResult(
                        stageResults,
                        stopwatch.Elapsed,
                        succeeded: false,
                        errorMessage: gateError,
                        plannedStageCount: stages.Count);

                    ReportCompleted(reporter, stopwatch.Elapsed, succeeded: false, gateError);
                    return failedResult;
                }
            }

            ReportCompleted(reporter, stopwatch.Elapsed, succeeded: true, errorMessage: null);
            return pipelineResult;
        }
        catch (OperationCanceledException ex) when (ex.InnerException is TokenBudgetExceededException budgetEx)
        {
            stopwatch.Stop();
            ReportCompleted(reporter, stopwatch.Elapsed, succeeded: false, budgetEx.Message);
            return new PipelineRunResult(
                stageResults,
                stopwatch.Elapsed,
                succeeded: false,
                errorMessage: budgetEx.Message,
                exception: budgetEx,
                plannedStageCount: stages.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            ReportCompleted(reporter, stopwatch.Elapsed, succeeded: false, "Cancelled");
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // HTTP timeouts and other non-user cancellations — treat as stage failure,
            // not as user cancellation. HttpClient.Timeout throws TaskCanceledException
            // which is OperationCanceledException, but the caller's token is NOT cancelled.
            stopwatch.Stop();
            var message = ex.InnerException is TimeoutException
                ? $"Stage timed out: {ex.InnerException.Message}"
                : $"Operation cancelled (not by caller): {ex.Message}";
            ReportCompleted(reporter, stopwatch.Elapsed, succeeded: false, message);
            return new PipelineRunResult(
                stageResults,
                stopwatch.Elapsed,
                succeeded: false,
                errorMessage: message,
                exception: ex,
                plannedStageCount: stages.Count);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            ReportCompleted(reporter, stopwatch.Elapsed, succeeded: false, ex.Message);
            return new PipelineRunResult(
                stageResults,
                stopwatch.Elapsed,
                succeeded: false,
                errorMessage: ex.Message,
                exception: ex,
                plannedStageCount: stages.Count);
        }
        finally
        {
            pipelineBudgetScope?.Dispose();
        }
    }

    /// <summary>
    /// Runs a phased pipeline where stages are grouped into named phases with
    /// lifecycle hooks and optional phase-level token budgets.
    /// </summary>
    /// <param name="workspace">The shared workspace for file I/O across stages.</param>
    /// <param name="phases">The ordered list of phases, each containing stages.</param>
    /// <param name="options">Optional pipeline-level configuration.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>An <see cref="IPipelineRunResult"/> describing the pipeline outcome.</returns>
    public Task<IPipelineRunResult> RunPhasedAsync(
        IWorkspace workspace,
        IReadOnlyList<PipelinePhase> phases,
        SequentialPipelineOptions? options,
        CancellationToken cancellationToken) =>
        RunPhasedCoreAsync(workspace, phases, pipelineState: null, options, cancellationToken);

    /// <summary>
    /// Runs a phased pipeline with a shared typed state object accessible to both
    /// phase lifecycle hooks and stage executors.
    /// </summary>
    /// <typeparam name="TState">The type of the shared pipeline state.</typeparam>
    /// <param name="workspace">The shared workspace for file I/O across stages.</param>
    /// <param name="phases">The ordered list of phases, each containing stages.</param>
    /// <param name="state">A shared state object accessible via
    /// <see cref="PhaseContext.GetRequiredState{T}"/> and
    /// <see cref="StageExecutionContext.GetRequiredState{T}"/>.</param>
    /// <param name="options">Optional pipeline-level configuration.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>An <see cref="IPipelineRunResult"/> describing the pipeline outcome.</returns>
    public Task<IPipelineRunResult> RunPhasedAsync<TState>(
        IWorkspace workspace,
        IReadOnlyList<PipelinePhase> phases,
        TState state,
        SequentialPipelineOptions? options,
        CancellationToken cancellationToken) where TState : class =>
        RunPhasedCoreAsync(workspace, phases, state, options, cancellationToken);

    private async Task<IPipelineRunResult> RunPhasedCoreAsync(
        IWorkspace workspace,
        IReadOnlyList<PipelinePhase> phases,
        object? pipelineState,
        SequentialPipelineOptions? options,
        CancellationToken cancellationToken)
    {
        var pipelineStopwatch = Stopwatch.StartNew();
        var reporter = _progressReporterFactory.Create(Guid.NewGuid().ToString("N"));
        var allStageResults = new List<IAgentStageResult>();
        var totalStages = phases.Sum(p => p.Stages.Count);
        var globalStageIndex = 0;

        reporter.Report(new WorkflowStartedEvent(
            DateTimeOffset.UtcNow,
            reporter.WorkflowId,
            reporter.AgentId,
            ParentAgentId: null,
            reporter.Depth,
            reporter.NextSequence()));

        IDisposable? pipelineBudgetScope = null;
        try
        {
            if (options?.TotalTokenBudget is { } totalBudget)
            {
                pipelineBudgetScope = _budgetTracker.BeginScope(totalBudget);
            }

            for (var phaseIndex = 0; phaseIndex < phases.Count; phaseIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var phase = phases[phaseIndex];
                var phasePolicy = phase.Policy;
                var phaseStopwatch = Stopwatch.StartNew();
                var phaseSucceeded = true;

                reporter.Report(new PhaseStartedEvent(
                    DateTimeOffset.UtcNow,
                    reporter.WorkflowId,
                    reporter.AgentId,
                    ParentAgentId: null,
                    reporter.Depth,
                    reporter.NextSequence(),
                    phase.Name,
                    phaseIndex,
                    phases.Count,
                    phase.Stages.Count));

                var phaseContext = new PhaseContext(
                    phase.Name,
                    phaseIndex,
                    phases.Count,
                    workspace,
                    pipelineState);

                IDisposable? phaseBudgetScope = null;
                try
                {
                    if (phasePolicy?.TokenBudget is { } phaseBudget)
                    {
                        phaseBudgetScope = _budgetTracker.BeginScope(phaseBudget);
                    }

                    if (phasePolicy?.OnEnterAsync is { } onEnter)
                    {
                        await onEnter(phaseContext, cancellationToken);
                    }

                    for (var stageInPhase = 0; stageInPhase < phase.Stages.Count; stageInPhase++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var stage = phase.Stages[stageInPhase];
                        var policy = stage.Policy;

                        var context = new StageExecutionContext(
                            workspace,
                            _diagnosticsAccessor,
                            reporter,
                            StageIndex: globalStageIndex,
                            TotalStages: totalStages,
                            StageName: stage.Name,
                            CallerCancellationToken: cancellationToken,
                            PipelineState: pipelineState,
                            PhaseName: phase.Name,
                            PhaseIndex: phaseIndex,
                            StageIndexInPhase: stageInPhase,
                            TotalStagesInPhase: phase.Stages.Count);

                        if (policy?.ShouldSkip?.Invoke(context) == true)
                        {
                            allStageResults.Add(new AgentStageResult(
                                stage.Name,
                                FinalResponse: null,
                                Diagnostics: null,
                                Outcome: StageOutcome.Skipped,
                                PhaseName: phase.Name));
                            globalStageIndex++;
                            continue;
                        }

                        reporter.Report(new AgentInvokedEvent(
                            DateTimeOffset.UtcNow,
                            reporter.WorkflowId,
                            stage.Name,
                            ParentAgentId: null,
                            reporter.Depth,
                            reporter.NextSequence(),
                            stage.Name));

                        var maxAttempts = policy?.MaxAttempts ?? 1;
                        StageExecutionResult? stageResult = null;
                        string? validationError = null;

                        IDisposable? stageBudgetScope = null;
                        try
                        {
                            if (policy?.TokenBudget is { } stageBudget)
                            {
                                stageBudgetScope = _budgetTracker.BeginChildScope(stage.Name, stageBudget);
                            }

                            for (var attempt = 0; attempt < maxAttempts; attempt++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                stageResult = await stage.Executor.ExecuteAsync(context, cancellationToken);

                                if (policy?.PostValidation is { } validate)
                                {
                                    validationError = validate(stageResult);
                                    if (validationError is null)
                                    {
                                        break;
                                    }

                                    if (attempt < maxAttempts - 1)
                                    {
                                        validationError = null;
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            var partialDiag = _diagnosticsAccessor.LastRunDiagnostics;
                            allStageResults.Add(new AgentStageResult(
                                stage.Name,
                                FinalResponse: null,
                                Diagnostics: partialDiag,
                                Outcome: StageOutcome.Failed,
                                PhaseName: phase.Name));

                            reporter.Report(new AgentFailedEvent(
                                DateTimeOffset.UtcNow,
                                reporter.WorkflowId,
                                stage.Name,
                                ParentAgentId: null,
                                reporter.Depth,
                                reporter.NextSequence(),
                                AgentName: stage.Name,
                                ErrorMessage: ex.Message));

                            throw;
                        }
                        finally
                        {
                            stageBudgetScope?.Dispose();
                        }

                        if (stageResult is not null && policy?.AfterExecution is { } afterExec)
                        {
                            await afterExec(stageResult, context);
                        }

                        if (validationError is not null)
                        {
                            throw new StageValidationException(stage.Name, validationError);
                        }

                        if (!stageResult!.Succeeded)
                        {
                            allStageResults.Add(new AgentStageResult(
                                stage.Name,
                                FinalResponse: null,
                                Diagnostics: stageResult.Diagnostics,
                                Outcome: StageOutcome.Failed,
                                PhaseName: phase.Name));

                            reporter.Report(new AgentFailedEvent(
                                DateTimeOffset.UtcNow,
                                reporter.WorkflowId,
                                stage.Name,
                                ParentAgentId: null,
                                reporter.Depth,
                                reporter.NextSequence(),
                                AgentName: stage.Name,
                                ErrorMessage: stageResult.Exception?.Message ?? "Stage failed"));

                            if (stageResult.FailureDisposition == FailureDisposition.AbortPipeline)
                            {
                                phaseSucceeded = false;
                                pipelineStopwatch.Stop();
                                var errorMsg = stageResult.Exception?.Message
                                    ?? $"Stage '{stage.Name}' failed";
                                ReportCompleted(reporter, pipelineStopwatch.Elapsed, succeeded: false, errorMsg);
                                return new PipelineRunResult(
                                    allStageResults,
                                    pipelineStopwatch.Elapsed,
                                    succeeded: false,
                                    errorMessage: errorMsg,
                                    exception: stageResult.Exception,
                                    plannedStageCount: totalStages);
                            }

                            globalStageIndex++;
                            continue;
                        }

                        ChatResponse? chatResponse = stageResult.ResponseText is not null
                            ? new ChatResponse(new ChatMessage(ChatRole.Assistant, stageResult.ResponseText))
                            : null;

                        allStageResults.Add(new AgentStageResult(
                            stage.Name,
                            chatResponse,
                            stageResult.Diagnostics,
                            PhaseName: phase.Name));

                        reporter.Report(new AgentCompletedEvent(
                            DateTimeOffset.UtcNow,
                            reporter.WorkflowId,
                            stage.Name,
                            ParentAgentId: null,
                            reporter.Depth,
                            reporter.NextSequence(),
                            stage.Name,
                            Duration: pipelineStopwatch.Elapsed,
                            TotalTokens: stageResult.Diagnostics?.AggregateTokenUsage.TotalTokens ?? 0));

                        globalStageIndex++;
                    }
                }
                finally
                {
                    phaseBudgetScope?.Dispose();

                    if (phasePolicy?.OnExitAsync is { } onExit)
                    {
                        await onExit(phaseContext, cancellationToken);
                    }

                    phaseStopwatch.Stop();
                    reporter.Report(new PhaseCompletedEvent(
                        DateTimeOffset.UtcNow,
                        reporter.WorkflowId,
                        reporter.AgentId,
                        ParentAgentId: null,
                        reporter.Depth,
                        reporter.NextSequence(),
                        phase.Name,
                        phaseIndex,
                        phases.Count,
                        phaseSucceeded,
                        phaseStopwatch.Elapsed));
                }
            }

            pipelineStopwatch.Stop();

            var pipelineResult = new PipelineRunResult(
                allStageResults,
                pipelineStopwatch.Elapsed,
                succeeded: true,
                errorMessage: null,
                plannedStageCount: totalStages);

            if (options?.CompletionGate is { } gate)
            {
                var gateError = gate(pipelineResult);
                if (gateError is not null)
                {
                    ReportCompleted(reporter, pipelineStopwatch.Elapsed, succeeded: false, gateError);
                    return new PipelineRunResult(
                        allStageResults,
                        pipelineStopwatch.Elapsed,
                        succeeded: false,
                        errorMessage: gateError,
                        plannedStageCount: totalStages);
                }
            }

            ReportCompleted(reporter, pipelineStopwatch.Elapsed, succeeded: true, errorMessage: null);
            return pipelineResult;
        }
        catch (OperationCanceledException ex) when (ex.InnerException is TokenBudgetExceededException budgetEx)
        {
            pipelineStopwatch.Stop();
            ReportCompleted(reporter, pipelineStopwatch.Elapsed, succeeded: false, budgetEx.Message);
            return new PipelineRunResult(
                allStageResults,
                pipelineStopwatch.Elapsed,
                succeeded: false,
                errorMessage: budgetEx.Message,
                exception: budgetEx,
                plannedStageCount: totalStages);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            pipelineStopwatch.Stop();
            ReportCompleted(reporter, pipelineStopwatch.Elapsed, succeeded: false, "Cancelled");
            throw;
        }
        catch (OperationCanceledException ex)
        {
            pipelineStopwatch.Stop();
            var message = ex.InnerException is TimeoutException
                ? $"Stage timed out: {ex.InnerException.Message}"
                : $"Operation cancelled (not by caller): {ex.Message}";
            ReportCompleted(reporter, pipelineStopwatch.Elapsed, succeeded: false, message);
            return new PipelineRunResult(
                allStageResults,
                pipelineStopwatch.Elapsed,
                succeeded: false,
                errorMessage: message,
                exception: ex,
                plannedStageCount: totalStages);
        }
        catch (Exception ex)
        {
            pipelineStopwatch.Stop();
            ReportCompleted(reporter, pipelineStopwatch.Elapsed, succeeded: false, ex.Message);
            return new PipelineRunResult(
                allStageResults,
                pipelineStopwatch.Elapsed,
                succeeded: false,
                errorMessage: ex.Message,
                exception: ex,
                plannedStageCount: totalStages);
        }
        finally
        {
            pipelineBudgetScope?.Dispose();
        }
    }

    private static void ReportCompleted(
        IProgressReporter reporter,
        TimeSpan duration,
        bool succeeded,
        string? errorMessage)
    {
        reporter.Report(new WorkflowCompletedEvent(
            DateTimeOffset.UtcNow,
            reporter.WorkflowId,
            reporter.AgentId,
            ParentAgentId: null,
            reporter.Depth,
            reporter.NextSequence(),
            succeeded,
            errorMessage,
            duration));
    }
}
