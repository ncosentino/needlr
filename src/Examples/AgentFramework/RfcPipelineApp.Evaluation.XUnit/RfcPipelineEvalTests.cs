using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Evaluation;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workspace;
using NexusLabs.Needlr.AgentFramework.Workflows.Budget;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.Copilot;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using RfcPipelineApp.Core;

using Xunit;

namespace RfcPipelineApp.Evaluation.XUnit;

/// <summary>
/// Evaluation tests for the RFC pipeline. Requires GitHub Copilot CLI auth.
/// Excluded from CI/CD — run locally with:
/// <c>dotnet test src/Examples/AgentFramework/RfcPipelineApp.Evaluation.XUnit/</c>
/// </summary>
public class RfcPipelineEvalTests : IAsyncLifetime
{
    private IServiceProvider _services = null!;
    private IPipelineRunResult _pipelineResult = null!;
    private IWorkspace _workspace = null!;

    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var copilotOptions = new CopilotChatClientOptions
        {
            DefaultModel = "claude-sonnet-4",
        };
        IChatClient chatClient = new CopilotChatClient(copilotOptions);

        _services = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .UsingChatClient(chatClient)
                .UsingDiagnostics()
                .UsingTokenBudget())
            .BuildServiceProvider(configuration);

        var agentFactory = _services.GetRequiredService<IAgentFactory>();
        var diagnosticsAccessor = _services.GetRequiredService<IAgentDiagnosticsAccessor>();
        var budgetTracker = _services.GetRequiredService<ITokenBudgetTracker>();
        var progressFactory = _services.GetRequiredService<IProgressReporterFactory>();
        var loggerFactory = _services.GetRequiredService<ILoggerFactory>();

        var assignment = new RfcAssignment(
            FeatureTitle: "Workspace File Versioning",
            Description: "Add automatic versioning to InMemoryWorkspace so every write creates a new version.",
            Constraints: ["Must be backward-compatible", "Memory overhead must be bounded"],
            ExistingContext: ["InMemoryWorkspace uses ConcurrentDictionary<string, string>"],
            TargetAudience: "engineering team");

        var metadata = new RfcMetadata();
        var stages = RfcPipelineBuilder.Build(
            assignment,
            agentFactory,
            metadata,
            loggerFactory.CreateLogger("RfcEval"));

        _workspace = new InMemoryWorkspace();

        var runner = new SequentialPipelineRunner(
            diagnosticsAccessor,
            budgetTracker,
            progressFactory);
        _pipelineResult = await runner.RunAsync(
            _workspace,
            stages,
            options: null,
            TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public void Pipeline_Succeeded()
    {
        Assert.True(
            _pipelineResult.Succeeded,
            $"Pipeline failed: {_pipelineResult.ErrorMessage}");
    }

    [Fact]
    public async Task Pipeline_StageMetrics_AllStagesRecorded()
    {
        var ctx = PipelineEvaluationContext.ForPipeline(_pipelineResult);
        var inputs = _pipelineResult.ToEvaluationInputs();

        var evaluator = new PipelineStageEvaluator();
        var result = await evaluator.EvaluateAsync(
            inputs.Messages,
            inputs.ModelResponse,
            additionalContext: [ctx],
            cancellationToken: TestContext.Current.CancellationToken);

        var totalStages = result.Metrics[PipelineStageEvaluator.TotalStagesMetricName]
            as NumericMetric;
        Assert.NotNull(totalStages);
        Assert.True(
            totalStages!.Value > 0,
            "Expected at least one stage to be recorded");

        var succeeded = result.Metrics[PipelineStageEvaluator.SucceededMetricName]
            as BooleanMetric;
        Assert.NotNull(succeeded);
        Assert.True(succeeded!.Value, "Pipeline stage evaluator reported failure");
    }

    [Fact]
    public async Task Pipeline_CostMetrics_TokensReported()
    {
        var ctx = PipelineEvaluationContext.ForPipeline(_pipelineResult);
        var inputs = _pipelineResult.ToEvaluationInputs();

        var evaluator = new PipelineCostEvaluator();
        var result = await evaluator.EvaluateAsync(
            inputs.Messages,
            inputs.ModelResponse,
            additionalContext: [ctx],
            cancellationToken: TestContext.Current.CancellationToken);

        var totalTokens = result.Metrics[PipelineCostEvaluator.TotalTokensMetricName]
            as NumericMetric;
        Assert.NotNull(totalTokens);
        Assert.True(
            totalTokens!.Value > 0,
            "Expected non-zero token usage");
    }

    [Fact]
    public async Task Pipeline_Trajectory_AllToolCallsSucceeded()
    {
        foreach (var stage in _pipelineResult.Stages
            .Where(s => s.Diagnostics is not null))
        {
            var stageCtx = PipelineEvaluationContext.ForStage(stage);
            if (stageCtx is null)
            {
                continue;
            }

            var stageInputs = stage.Diagnostics!.ToEvaluationInputs();
            var evaluator = new ToolCallTrajectoryEvaluator();
            var result = await evaluator.EvaluateAsync(
                stageInputs.Messages,
                stageInputs.ModelResponse,
                additionalContext: [stageCtx],
                cancellationToken: TestContext.Current.CancellationToken);

            var allSucceeded = result.Metrics[ToolCallTrajectoryEvaluator.AllSucceededMetricName]
                as BooleanMetric;
            Assert.True(
                allSucceeded?.Value ?? true,
                $"Stage '{stage.AgentName}' had failed tool calls");
        }
    }

    [Fact]
    public void Pipeline_ProducedRfcDraft()
    {
        Assert.True(
            _workspace.FileExists("rfc-draft.md"),
            "RFC draft was not produced");

        var draft = _workspace.TryReadFile("rfc-draft.md");
        Assert.True(draft.Success, "Failed to read RFC draft from workspace");
        Assert.True(
            draft.Value.Content.Length > 100,
            "RFC draft is suspiciously short");
    }

    [Fact]
    public async Task Pipeline_QualityGate_PassesBasicThresholds()
    {
        var ctx = PipelineEvaluationContext.ForPipeline(_pipelineResult);
        var inputs = _pipelineResult.ToEvaluationInputs();

        var stageEvaluator = new PipelineStageEvaluator();
        var stageResult = await stageEvaluator.EvaluateAsync(
            inputs.Messages,
            inputs.ModelResponse,
            additionalContext: [ctx],
            cancellationToken: TestContext.Current.CancellationToken);

        var costEvaluator = new PipelineCostEvaluator();
        var costResult = await costEvaluator.EvaluateAsync(
            inputs.Messages,
            inputs.ModelResponse,
            additionalContext: [ctx],
            cancellationToken: TestContext.Current.CancellationToken);

        var gate = new EvaluationQualityGate()
            .RequireBoolean(PipelineStageEvaluator.SucceededMetricName, expected: true)
            .RequireNumericMin(PipelineStageEvaluator.TotalStagesMetricName, min: 1)
            .RequireNumericMin(PipelineCostEvaluator.TotalTokensMetricName, min: 1);

        gate.Assert(stageResult, costResult);
    }
}
