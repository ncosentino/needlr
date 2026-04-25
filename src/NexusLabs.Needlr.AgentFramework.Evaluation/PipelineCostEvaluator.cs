using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Deterministic evaluator that scores token usage and cost breakdown per stage of a
/// pipeline run from the captured <see cref="IPipelineRunResult"/> snapshot carried in a
/// <see cref="PipelineEvaluationContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// This evaluator never contacts a language model. It reads
/// <see cref="IPipelineRunResult.AggregateTokenUsage"/> and per-stage
/// <see cref="IAgentRunDiagnostics.AggregateTokenUsage"/> to produce:
/// </para>
/// <list type="bullet">
///   <item><description><c>pipeline.total_tokens</c> — sum of all stage tokens.</description></item>
///   <item><description><c>pipeline.total_input_tokens</c> — aggregate input tokens.</description></item>
///   <item><description><c>pipeline.total_output_tokens</c> — aggregate output tokens.</description></item>
///   <item><description><c>pipeline.stage_count</c> — number of stages in the pipeline.</description></item>
///   <item><description><c>pipeline.stages_with_diagnostics</c> — count of stages that have non-null diagnostics.</description></item>
///   <item><description><c>pipeline.most_expensive_stage</c> — name of the stage with the most tokens.</description></item>
///   <item><description><c>pipeline.most_expensive_stage_pct</c> — percentage of total tokens used by the most expensive stage.</description></item>
/// </list>
/// <para>
/// When no <see cref="PipelineEvaluationContext"/> is present in the
/// <c>additionalContext</c> collection, the evaluator returns an empty
/// <see cref="EvaluationResult"/> — callers should treat that as "not applicable".
/// </para>
/// </remarks>
public sealed class PipelineCostEvaluator : IEvaluator
{
    /// <summary>Metric name for the total token count across all stages.</summary>
    public const string TotalTokensMetricName = "pipeline.total_tokens";

    /// <summary>Metric name for the total input token count.</summary>
    public const string TotalInputTokensMetricName = "pipeline.total_input_tokens";

    /// <summary>Metric name for the total output token count.</summary>
    public const string TotalOutputTokensMetricName = "pipeline.total_output_tokens";

    /// <summary>Metric name for the number of stages in the pipeline.</summary>
    public const string StageCountMetricName = "pipeline.stage_count";

    /// <summary>Metric name for the count of stages that have diagnostics.</summary>
    public const string StagesWithDiagnosticsMetricName = "pipeline.stages_with_diagnostics";

    /// <summary>Metric name for the name of the most expensive stage by token count.</summary>
    public const string MostExpensiveStageMetricName = "pipeline.most_expensive_stage";

    /// <summary>Metric name for the percentage of total tokens used by the most expensive stage.</summary>
    public const string MostExpensiveStagePctMetricName = "pipeline.most_expensive_stage_pct";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        TotalTokensMetricName,
        TotalInputTokensMetricName,
        TotalOutputTokensMetricName,
        StageCountMetricName,
        StagesWithDiagnosticsMetricName,
        MostExpensiveStageMetricName,
        MostExpensiveStagePctMetricName,
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
        var stageCount = stages.Count;

        long totalTokens = 0;
        long totalInputTokens = 0;
        long totalOutputTokens = 0;
        var stagesWithDiagnostics = 0;
        string? mostExpensiveStageName = null;
        long mostExpensiveStageTokens = 0;

        for (var i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            if (stage.Diagnostics is null)
            {
                continue;
            }

            stagesWithDiagnostics++;
            var usage = stage.Diagnostics.AggregateTokenUsage;
            totalTokens += usage.TotalTokens;
            totalInputTokens += usage.InputTokens;
            totalOutputTokens += usage.OutputTokens;

            if (usage.TotalTokens > mostExpensiveStageTokens)
            {
                mostExpensiveStageTokens = usage.TotalTokens;
                mostExpensiveStageName = stage.AgentName;
            }
        }

        var mostExpensivePct = totalTokens > 0
            ? (double)mostExpensiveStageTokens / totalTokens * 100.0
            : 0;

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            new NumericMetric(
                TotalTokensMetricName,
                value: totalTokens,
                reason: $"{totalTokens:N0} total tokens consumed across {stagesWithDiagnostics} stage(s) with diagnostics."),
            new NumericMetric(
                TotalInputTokensMetricName,
                value: totalInputTokens,
                reason: $"{totalInputTokens:N0} input tokens consumed."),
            new NumericMetric(
                TotalOutputTokensMetricName,
                value: totalOutputTokens,
                reason: $"{totalOutputTokens:N0} output tokens consumed."),
            new NumericMetric(
                StageCountMetricName,
                value: stageCount,
                reason: $"Pipeline has {stageCount} stage(s)."),
            new NumericMetric(
                StagesWithDiagnosticsMetricName,
                value: stagesWithDiagnostics,
                reason: stagesWithDiagnostics == stageCount
                    ? "All stages have diagnostics."
                    : $"{stagesWithDiagnostics} of {stageCount} stage(s) have diagnostics."),
            new StringMetric(
                MostExpensiveStageMetricName,
                value: mostExpensiveStageName ?? string.Empty,
                reason: mostExpensiveStageName is not null
                    ? $"Stage '{mostExpensiveStageName}' used the most tokens ({mostExpensiveStageTokens:N0})."
                    : "No stages have diagnostics to determine the most expensive stage."),
            new NumericMetric(
                MostExpensiveStagePctMetricName,
                value: mostExpensivePct,
                reason: mostExpensiveStageName is not null
                    ? $"Stage '{mostExpensiveStageName}' consumed {mostExpensivePct:F1}% of total tokens."
                    : "No stages have diagnostics to compute percentage.")));
    }
}
