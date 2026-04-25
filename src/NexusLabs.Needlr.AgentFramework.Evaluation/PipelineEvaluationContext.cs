using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Carries an <see cref="IPipelineRunResult"/> through the
/// <c>Microsoft.Extensions.AI.Evaluation</c> evaluator pipeline so that pipeline-aware
/// evaluators can score per-stage and aggregate metrics without re-invoking the model.
/// </summary>
/// <remarks>
/// <para>
/// Evaluators that require the full pipeline result look up the single instance of this
/// context in the <c>additionalContext</c> collection passed to
/// <see cref="IEvaluator.EvaluateAsync"/>.
/// </para>
/// <para>
/// <see cref="EvaluationContext.Contents"/> contains a single <see cref="TextContent"/>
/// summarising the pipeline run so that reporting pipelines which only serialise
/// <see cref="EvaluationContext.Contents"/> still record meaningful information.
/// Consumers that need the full snapshot read <see cref="PipelineResult"/> directly.
/// </para>
/// <para>
/// Use <see cref="ForStage"/> to create a per-stage
/// <see cref="AgentRunDiagnosticsContext"/> for evaluators that operate on individual
/// agent runs within the pipeline.
/// </para>
/// </remarks>
public sealed class PipelineEvaluationContext : EvaluationContext
{
    /// <summary>
    /// The stable name used for this context. Evaluators can locate the context by
    /// matching <see cref="EvaluationContext.Name"/> against this value.
    /// </summary>
    public const string ContextName = "Needlr Pipeline Run Result";

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineEvaluationContext"/> class.
    /// </summary>
    /// <param name="pipelineResult">The captured pipeline run result to expose to evaluators.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pipelineResult"/> is <see langword="null"/>.</exception>
    public PipelineEvaluationContext(IPipelineRunResult pipelineResult)
        : base(ContextName, BuildContents(pipelineResult))
    {
        PipelineResult = pipelineResult;
    }

    /// <summary>Gets the pipeline run result.</summary>
    public IPipelineRunResult PipelineResult { get; }

    /// <summary>
    /// Creates an <see cref="AgentRunDiagnosticsContext"/> for a single stage within a
    /// pipeline, or <see langword="null"/> when the stage has no captured diagnostics.
    /// </summary>
    /// <param name="stage">The stage result to convert.</param>
    /// <returns>
    /// An <see cref="AgentRunDiagnosticsContext"/> wrapping the stage's diagnostics, or
    /// <see langword="null"/> if <see cref="IAgentStageResult.Diagnostics"/> is
    /// <see langword="null"/>.
    /// </returns>
    public static AgentRunDiagnosticsContext? ForStage(IAgentStageResult stage)
    {
        return stage.Diagnostics is not null
            ? new AgentRunDiagnosticsContext(stage.Diagnostics)
            : null;
    }

    /// <summary>
    /// Creates a pipeline-level context from a full pipeline result.
    /// </summary>
    /// <param name="result">The pipeline run result.</param>
    /// <returns>A new <see cref="PipelineEvaluationContext"/> wrapping the result.</returns>
    public static PipelineEvaluationContext ForPipeline(IPipelineRunResult result) =>
        new(result);

    private static AIContent[] BuildContents(IPipelineRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var summary =
            $"Pipeline Succeeded={result.Succeeded} " +
            $"Stages={result.Stages.Count} " +
            $"Duration={result.TotalDuration.TotalMilliseconds:F0}ms " +
            $"TotalTokens={result.AggregateTokenUsage?.TotalTokens ?? 0}";

        return [new TextContent(summary)];
    }
}
