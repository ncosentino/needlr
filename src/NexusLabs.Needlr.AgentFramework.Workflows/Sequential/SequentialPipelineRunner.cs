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
    public async Task<IPipelineRunResult> RunAsync(
        IWorkspace workspace,
        IReadOnlyList<PipelineStage> stages,
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
                    CallerCancellationToken: cancellationToken);

                // Evaluate ShouldSkip
                if (policy?.ShouldSkip?.Invoke(context) == true)
                {
                    stageResults.Add(new AgentStageResult(
                        stage.Name,
                        FinalResponse: null,
                        Diagnostics: null));
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
                        Diagnostics: partialDiag));

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

                if (validationError is not null)
                {
                    throw new StageValidationException(stage.Name, validationError);
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
