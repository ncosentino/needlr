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

namespace RfcPipelineApp.Evaluation.TUnit;

/// <summary>
/// Evaluation tests for the RFC pipeline. Requires GitHub Copilot CLI auth.
/// Excluded from CI/CD — run locally with:
/// <c>dotnet test src/Examples/AgentFramework/RfcPipelineApp.Evaluation.TUnit/</c>
/// </summary>
public class RfcPipelineEvalTests
{
    private static readonly SemaphoreSlim _initLock = new(1, 1);
    private static IServiceProvider? _services;
    private static IPipelineRunResult? _pipelineResultField;
    private static IWorkspace? _workspaceField;

    private IPipelineRunResult PipelineResult => _pipelineResultField
        ?? throw new InvalidOperationException("Pipeline has not been initialized.");
    private IWorkspace Workspace => _workspaceField
        ?? throw new InvalidOperationException("Workspace has not been initialized.");

    [Before(Test)]
    public async Task EnsurePipelineRan()
    {
        if (_pipelineResultField is not null)
        {
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_pipelineResultField is not null)
            {
                return;
            }

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
                .UsingPostPluginRegistrationCallback(services =>
                {
                    services.AddLogging();
                })
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
            var state = new RfcPipelineState(metadata);
            var stages = RfcPipelineBuilder.Build(
                assignment,
                agentFactory,
                state,
                loggerFactory.CreateLogger("RfcEval"));

            _workspaceField = new InMemoryWorkspace();

            var runner = new SequentialPipelineRunner(
                diagnosticsAccessor,
                budgetTracker,
                progressFactory);
            _pipelineResultField = await runner.RunAsync(
                _workspaceField,
                stages,
                state,
                options: null,
                CancellationToken.None);
        }
        finally
        {
            _initLock.Release();
        }
    }

    [Test]
    public async Task Pipeline_Succeeded()
    {
        await Assert.That(PipelineResult.Succeeded).IsTrue();
    }

    [Test]
    public async Task Pipeline_StageMetrics_AllStagesRecorded()
    {
        var ctx = PipelineEvaluationContext.ForPipeline(PipelineResult);
        var inputs = PipelineResult.ToEvaluationInputs();

        var evaluator = new PipelineStageEvaluator();
        var result = await evaluator.EvaluateAsync(
            inputs.Messages,
            inputs.ModelResponse,
            additionalContext: [ctx]);

        var totalStages = result.Metrics[PipelineStageEvaluator.TotalStagesMetricName]
            as NumericMetric;
        await Assert.That(totalStages).IsNotNull();
        await Assert.That(totalStages!.Value > 0).IsTrue();

        var succeeded = result.Metrics[PipelineStageEvaluator.SucceededMetricName]
            as BooleanMetric;
        await Assert.That(succeeded).IsNotNull();
        await Assert.That(succeeded!.Value).IsTrue();
    }

    [Test]
    public async Task Pipeline_CostMetrics_TokensReported()
    {
        var ctx = PipelineEvaluationContext.ForPipeline(PipelineResult);
        var inputs = PipelineResult.ToEvaluationInputs();

        var evaluator = new PipelineCostEvaluator();
        var result = await evaluator.EvaluateAsync(
            inputs.Messages,
            inputs.ModelResponse,
            additionalContext: [ctx]);

        var totalTokens = result.Metrics[PipelineCostEvaluator.TotalTokensMetricName]
            as NumericMetric;
        await Assert.That(totalTokens).IsNotNull();
        await Assert.That(totalTokens!.Value > 0).IsTrue();
    }

    [Test]
    public async Task Pipeline_Trajectory_AllToolCallsSucceeded()
    {
        foreach (var stage in PipelineResult.Stages
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
                additionalContext: [stageCtx]);

            var allSucceeded = result.Metrics[ToolCallTrajectoryEvaluator.AllSucceededMetricName]
                as BooleanMetric;
            await Assert.That(allSucceeded?.Value ?? true).IsTrue();
        }
    }

    [Test]
    public async Task Pipeline_ProducedRfcDraft()
    {
        await Assert.That(Workspace.FileExists("rfc-draft.md")).IsTrue();

        var draft = Workspace.TryReadFile("rfc-draft.md");
        await Assert.That(draft.Success).IsTrue();
        await Assert.That(draft.Value.Content.Length > 100).IsTrue();
    }

    [Test]
    public async Task Pipeline_QualityGate_PassesBasicThresholds()
    {
        var ctx = PipelineEvaluationContext.ForPipeline(PipelineResult);
        var inputs = PipelineResult.ToEvaluationInputs();

        var stageEvaluator = new PipelineStageEvaluator();
        var stageResult = await stageEvaluator.EvaluateAsync(
            inputs.Messages,
            inputs.ModelResponse,
            additionalContext: [ctx]);

        var costEvaluator = new PipelineCostEvaluator();
        var costResult = await costEvaluator.EvaluateAsync(
            inputs.Messages,
            inputs.ModelResponse,
            additionalContext: [ctx]);

        var gate = new EvaluationQualityGate()
            .RequireBoolean(PipelineStageEvaluator.SucceededMetricName, expected: true)
            .RequireNumericMin(PipelineStageEvaluator.TotalStagesMetricName, min: 1)
            .RequireNumericMin(PipelineCostEvaluator.TotalTokensMetricName, min: 1);

        gate.Assert(stageResult, costResult);
    }
}
