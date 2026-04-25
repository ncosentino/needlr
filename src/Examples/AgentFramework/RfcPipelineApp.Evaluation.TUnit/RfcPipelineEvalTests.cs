using NexusLabs.Needlr.AgentFramework.Evaluation;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace RfcPipelineApp.Evaluation.TUnit;

/// <summary>
/// Evaluation tests for the RFC pipeline. These require an LLM provider
/// (configured via appsettings.json) and are excluded from CI/CD runs.
/// Run locally with: dotnet test src/Examples/AgentFramework/RfcPipelineApp.Evaluation.TUnit/
/// </summary>
public class RfcPipelineEvalTests
{
    [Test]
    public async Task Pipeline_AllStagesSucceed_TrajectoryAllSucceeded()
    {
        // NOTE: This test requires an LLM provider. It will fail in CI/CD.
        // Run locally after configuring appsettings.json with your provider.

        // 1. Build service provider + pipeline
        // 2. Run pipeline
        // 3. Bridge to evaluators via PipelineEvaluationContext
        // 4. Assert trajectory metrics

        // For now, this is a SKELETON showing the wiring pattern.
        // The actual LLM execution is deferred until a provider is configured.
        await Task.CompletedTask;

        // Example of what the full test looks like:
        // var result = await runner.RunAsync(workspace, stages, options, ct);
        // var ctx = PipelineEvaluationContext.ForPipeline(result);
        // var eval = new ToolCallTrajectoryEvaluator();
        // var metrics = await eval.EvaluateAsync(inputs.Messages, inputs.ModelResponse, additionalContext: [ctx]);
        // var allSucceeded = metrics.Get<BooleanMetric>(ToolCallTrajectoryEvaluator.AllSucceededMetricName);
        // Assert allSucceeded.Value is true
    }

    [Test]
    public async Task Pipeline_CostMetrics_UnderBudget()
    {
        // Skeleton — same pattern as above but with PipelineCostEvaluator
        await Task.CompletedTask;

        // Example of what the full test looks like:
        // var result = await runner.RunAsync(workspace, stages, options, ct);
        // var ctx = PipelineEvaluationContext.ForPipeline(result);
        // var eval = new PipelineCostEvaluator();
        // var metrics = await eval.EvaluateAsync(inputs.Messages, inputs.ModelResponse, additionalContext: [ctx]);
        // Assert cost metrics are within budget thresholds
    }

    [Test]
    public async Task Pipeline_StageMetrics_AllStagesComplete()
    {
        // Skeleton — same pattern with PipelineStageEvaluator
        await Task.CompletedTask;

        // Example of what the full test looks like:
        // var result = await runner.RunAsync(workspace, stages, options, ct);
        // var ctx = PipelineEvaluationContext.ForPipeline(result);
        // var eval = new PipelineStageEvaluator();
        // var metrics = await eval.EvaluateAsync(inputs.Messages, inputs.ModelResponse, additionalContext: [ctx]);
        // Assert all stages completed successfully
    }
}
