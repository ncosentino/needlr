using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Deterministic evaluator that scores per-stage success/failure and overall pipeline
/// health from the captured <see cref="IPipelineRunResult"/> snapshot carried in a
/// <see cref="PipelineEvaluationContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// This evaluator never contacts a language model. It reads the
/// <see cref="IPipelineRunResult"/> to produce:
/// </para>
/// <list type="bullet">
///   <item><description><c>pipeline.succeeded</c> — whether the pipeline succeeded.</description></item>
///   <item><description><c>pipeline.total_stages</c> — total number of stages.</description></item>
///   <item><description><c>pipeline.completed_stages</c> — stages with non-null diagnostics.</description></item>
///   <item><description><c>pipeline.skipped_stages</c> — stages with null diagnostics AND null response.</description></item>
///   <item><description><c>pipeline.total_duration_ms</c> — total pipeline duration in milliseconds.</description></item>
///   <item><description><c>pipeline.error_message</c> — error message if the pipeline failed (nullable).</description></item>
/// </list>
/// <para>
/// When no <see cref="PipelineEvaluationContext"/> is present in the
/// <c>additionalContext</c> collection, the evaluator returns an empty
/// <see cref="EvaluationResult"/> — callers should treat that as "not applicable".
/// </para>
/// </remarks>
public sealed class PipelineStageEvaluator : IEvaluator
{
    /// <summary>Metric name for whether the pipeline succeeded.</summary>
    public const string SucceededMetricName = "pipeline.succeeded";

    /// <summary>Metric name for the total number of stages.</summary>
    public const string TotalStagesMetricName = "pipeline.total_stages";

    /// <summary>Metric name for the number of completed stages (those with diagnostics).</summary>
    public const string CompletedStagesMetricName = "pipeline.completed_stages";

    /// <summary>Metric name for the number of skipped stages (null diagnostics AND null response).</summary>
    public const string SkippedStagesMetricName = "pipeline.skipped_stages";

    /// <summary>Metric name for the total pipeline duration in milliseconds.</summary>
    public const string TotalDurationMsMetricName = "pipeline.total_duration_ms";

    /// <summary>Metric name for the error message if the pipeline failed.</summary>
    public const string ErrorMessageMetricName = "pipeline.error_message";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        SucceededMetricName,
        TotalStagesMetricName,
        CompletedStagesMetricName,
        SkippedStagesMetricName,
        TotalDurationMsMetricName,
        ErrorMessageMetricName,
    ];

    /// <inheritdoc />
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var pipelineResult = additionalContext?
            .OfType<PipelineEvaluationContext>()
            .FirstOrDefault()?
            .PipelineResult;

        if (pipelineResult is null)
        {
            return new ValueTask<EvaluationResult>(new EvaluationResult());
        }

        var stages = pipelineResult.Stages;
        var totalStages = pipelineResult.PlannedStageCount;
        var completedStages = 0;
        var skippedStages = 0;

        for (var i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            if (stage.Diagnostics is not null)
            {
                completedStages++;
            }
            else if (stage.FinalResponse is null)
            {
                skippedStages++;
            }
        }

        var durationMs = pipelineResult.TotalDuration.TotalMilliseconds;
        var succeeded = pipelineResult.Succeeded;
        var errorMessage = pipelineResult.ErrorMessage;

        var metrics = new List<EvaluationMetric>
        {
            new BooleanMetric(
                SucceededMetricName,
                value: succeeded,
                reason: succeeded
                    ? "Pipeline completed successfully."
                    : "Pipeline did not complete successfully."),
            new NumericMetric(
                TotalStagesMetricName,
                value: totalStages,
                reason: $"Pipeline has {totalStages} stage(s)."),
            new NumericMetric(
                CompletedStagesMetricName,
                value: completedStages,
                reason: completedStages == totalStages
                    ? "All stages completed with diagnostics."
                    : $"{completedStages} of {totalStages} stage(s) completed with diagnostics."),
            new NumericMetric(
                SkippedStagesMetricName,
                value: skippedStages,
                reason: skippedStages == 0
                    ? "No stages were skipped."
                    : $"{skippedStages} stage(s) were skipped (no diagnostics and no response)."),
            new NumericMetric(
                TotalDurationMsMetricName,
                value: durationMs,
                reason: $"Pipeline ran for {durationMs:F0}ms."),
            new StringMetric(
                ErrorMessageMetricName,
                value: errorMessage,
                reason: errorMessage is not null
                    ? $"Pipeline error: {errorMessage}"
                    : "No error occurred."),
        };

        return new ValueTask<EvaluationResult>(new EvaluationResult(metrics.ToArray()));
    }
}
