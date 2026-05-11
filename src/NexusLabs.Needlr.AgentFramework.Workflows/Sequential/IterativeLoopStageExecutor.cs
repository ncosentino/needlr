using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Executes a pipeline stage by running an <see cref="IIterativeAgentLoop"/> with
/// dynamically constructed options and context.
/// </summary>
/// <remarks>
/// <para>
/// This executor bridges the workspace-driven iterative loop pattern into the
/// <see cref="IStageExecutor"/> contract used by <see cref="SequentialPipelineRunner"/>.
/// Unlike <see cref="AgentStageExecutor"/> (which wraps a single-pass
/// <c>AIAgent.RunAsync</c> call), this executor runs a multi-iteration loop where each
/// iteration builds a fresh prompt from workspace state, maintaining O(n) token cost.
/// </para>
/// <para>
/// All termination paths use result-based signaling — the executor never throws
/// exceptions for loop-level failures. This means exception-driven decorators like
/// <see cref="ContinueOnFailureExecutor"/> and <see cref="FallbackExecutor"/> do not
/// intercept loop termination results. For advisory behavior, set
/// <c>failureDisposition</c> to <see cref="FailureDisposition.ContinueAdvisory"/>.
/// For timeout enforcement, <see cref="TimeoutExecutor"/> still works because the loop
/// observes the linked <see cref="CancellationToken"/> and terminates cooperatively.
/// </para>
/// <para>
/// On every successful loop completion, the executor maps
/// <see cref="IterativeLoopResult.Termination"/> (a <see cref="TerminationReason"/>
/// enum) to a typed <see cref="StageTermination"/> case and surfaces it via
/// <see cref="StageExecutionResult.Termination"/>. The
/// <c>onLoopCompleted</c> callback can return a <see cref="StageTermination"/> to
/// override the framework-mapped default (e.g. to attach app-specific narrative as
/// a <see cref="StageTermination.Custom"/> case); returning <see langword="null"/>
/// uses the framework default.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Basic usage
/// var executor = new IterativeLoopStageExecutor(
///     iterativeLoop,
///     ctx =&gt; new IterativeLoopOptions
///     {
///         Instructions = "Write an article.",
///         Tools = tools,
///         PromptFactory = iterCtx =&gt; BuildPrompt(iterCtx.Workspace),
///         MaxIterations = 15,
///         LoopName = ctx.StageName,
///     });
///
/// // With onLoopCompleted to override the framework-mapped termination
/// var executor = new IterativeLoopStageExecutor(
///     iterativeLoop,
///     ctx =&gt; buildOptions(ctx),
///     onLoopCompleted: (loopResult, ctx) =&gt;
///     {
///         accessor.LastDiagnostics = loopResult.Diagnostics;
///         // Return a Custom termination to attach app narrative + metadata.
///         return new StageTermination.Custom(
///             Reason: "Reconciled",
///             Properties: new Dictionary&lt;string, object?&gt; { ["FindingCount"] = 7 });
///     });
///
/// // With shouldTreatAsSuccess for acceptable non-success terminations
/// var executor = new IterativeLoopStageExecutor(
///     iterativeLoop,
///     ctx =&gt; buildOptions(ctx),
///     shouldTreatAsSuccess: r =&gt;
///         r.Termination is TerminationReason.MaxIterationsReached
///                       or TerminationReason.MaxToolCallsReached);
///
/// // Composing with decorators
/// var timedExecutor = new TimeoutExecutor(
///     new IterativeLoopStageExecutor(loop, optionsFactory),
///     TimeSpan.FromMinutes(10));
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed class IterativeLoopStageExecutor : IStageExecutor
{
    private readonly IIterativeAgentLoop _loop;
    private readonly Func<StageExecutionContext, IterativeLoopOptions> _optionsFactory;
    private readonly Func<StageExecutionContext, IterativeContext>? _contextFactory;
    private readonly Func<IterativeLoopResult, StageExecutionContext, StageTermination?>? _onLoopCompleted;
    private readonly Func<IterativeLoopResult, bool>? _shouldTreatAsSuccess;
    private readonly FailureDisposition _failureDisposition;

    /// <summary>
    /// Initializes a new <see cref="IterativeLoopStageExecutor"/>.
    /// </summary>
    /// <param name="loop">The iterative agent loop to execute.</param>
    /// <param name="optionsFactory">
    /// Factory that produces the <see cref="IterativeLoopOptions"/> from the current stage
    /// context. Called once per execution — callers configure instructions, tools, prompt
    /// factory, iteration limits, and all other loop settings here.
    /// </param>
    /// <param name="contextFactory">
    /// Optional factory that produces the <see cref="IterativeContext"/> from the current stage
    /// context. When <see langword="null"/> (the default), the executor creates an
    /// <see cref="IterativeContext"/> using <see cref="StageExecutionContext.Workspace"/>.
    /// Provide a factory to pre-populate <see cref="IterativeContext.State"/> or use a
    /// different workspace.
    /// </param>
    /// <param name="onLoopCompleted">
    /// Optional callback invoked immediately after the loop completes, before result mapping.
    /// Receives the raw <see cref="IterativeLoopResult"/> and the <see cref="StageExecutionContext"/>.
    /// May return a <see cref="StageTermination"/> to override the framework-mapped default
    /// (e.g. to attach app narrative as a <see cref="StageTermination.Custom"/> case);
    /// returning <see langword="null"/> uses the framework default mapped from
    /// <see cref="IterativeLoopResult.Termination"/>.
    /// Called on both success and failure paths. Not called if the loop throws an exception.
    /// </param>
    /// <param name="shouldTreatAsSuccess">
    /// Optional predicate evaluated when the loop result has
    /// <see cref="IterativeLoopResult.Succeeded"/> = <see langword="false"/>. When the
    /// predicate returns <see langword="true"/>, the executor treats the result as a success.
    /// Use this for termination reasons like <see cref="TerminationReason.MaxIterationsReached"/>
    /// that are acceptable in the caller's domain. The reported
    /// <see cref="StageExecutionResult.Termination"/> still reflects the loop's actual
    /// termination — only the success/failure outcome is flipped.
    /// Not called when the loop already succeeded.
    /// </param>
    /// <param name="failureDisposition">
    /// The <see cref="FailureDisposition"/> applied to failed results. Defaults to
    /// <see cref="FailureDisposition.AbortPipeline"/>. Set to
    /// <see cref="FailureDisposition.ContinueAdvisory"/> for stages whose failure should
    /// not halt the pipeline.
    /// </param>
    public IterativeLoopStageExecutor(
        IIterativeAgentLoop loop,
        Func<StageExecutionContext, IterativeLoopOptions> optionsFactory,
        Func<StageExecutionContext, IterativeContext>? contextFactory = null,
        Func<IterativeLoopResult, StageExecutionContext, StageTermination?>? onLoopCompleted = null,
        Func<IterativeLoopResult, bool>? shouldTreatAsSuccess = null,
        FailureDisposition failureDisposition = FailureDisposition.AbortPipeline)
    {
        _loop = loop;
        _optionsFactory = optionsFactory;
        _contextFactory = contextFactory;
        _onLoopCompleted = onLoopCompleted;
        _shouldTreatAsSuccess = shouldTreatAsSuccess;
        _failureDisposition = failureDisposition;
    }

    /// <inheritdoc />
    public async Task<StageExecutionResult> ExecuteAsync(
        StageExecutionContext context,
        CancellationToken cancellationToken)
    {
        var options = _optionsFactory(context);
        var iterativeContext = _contextFactory?.Invoke(context)
            ?? new IterativeContext { Workspace = context.Workspace };

        using (context.DiagnosticsAccessor.BeginCapture())
        {
            var loopResult = await _loop.RunAsync(options, iterativeContext, cancellationToken);
            var diagnostics = loopResult.Diagnostics
                ?? context.DiagnosticsAccessor.LastRunDiagnostics;

            var mappedTermination = MapTermination(loopResult);
            var overriddenTermination = _onLoopCompleted?.Invoke(loopResult, context);
            var termination = overriddenTermination ?? mappedTermination;

            var succeeded = loopResult.Succeeded
                || (_shouldTreatAsSuccess?.Invoke(loopResult) == true);

            if (succeeded)
            {
                return StageExecutionResult.Success(
                    context.StageName,
                    diagnostics,
                    loopResult.FinalResponse?.Text,
                    termination: termination);
            }

            var failureException = new InvalidOperationException(
                $"{context.StageName} terminated [{loopResult.Termination}] after " +
                $"{loopResult.Iterations.Count} iteration(s): {loopResult.ErrorMessage}");
            return StageExecutionResult.Failed(
                context.StageName,
                failureException,
                diagnostics,
                _failureDisposition,
                termination: termination);
        }
    }

    private static StageTermination MapTermination(IterativeLoopResult loopResult)
    {
        var cfg = loopResult.Configuration;
        return loopResult.Termination switch
        {
            TerminationReason.Completed => new StageTermination.Completed(),
            TerminationReason.NaturalCompletion => new StageTermination.NaturalCompletion(),
            TerminationReason.CompletedEarlyAfterToolCall => new StageTermination.CompletedEarlyAfterToolCall(),
            TerminationReason.MaxIterationsReached => new StageTermination.MaxIterationsReached(
                Limit: cfg.MaxIterations,
                IterationsUsed: loopResult.Iterations.Count),
            TerminationReason.MaxToolCallsReached => new StageTermination.MaxToolCallsReached(
                Limit: cfg.MaxTotalToolCalls ?? int.MaxValue,
                ToolCallsUsed: loopResult.Iterations.Sum(i => i.ToolCallCount)),
            TerminationReason.BudgetPressure => new StageTermination.BudgetPressure(
                Threshold: cfg.BudgetPressureThreshold),
            TerminationReason.Cancelled => new StageTermination.Cancelled(),
            TerminationReason.Error => new StageTermination.Failed(
                new InvalidOperationException(loopResult.ErrorMessage ?? "loop reported error")),
            TerminationReason.StallDetected => new StageTermination.StallDetected(
                ConsecutiveThreshold: cfg.StallDetection?.ConsecutiveThreshold),
            _ => throw new InvalidOperationException(
                $"Unknown TerminationReason value '{loopResult.Termination}' — add a mapping arm to {nameof(IterativeLoopStageExecutor)}.{nameof(MapTermination)}."),
        };
    }
}
