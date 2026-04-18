using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class PipelineEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task EvaluateAsync_NullPipelineResult_ThrowsArgumentNullException()
    {
        var config = new ChatConfiguration(new ThrowingChatClient());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PipelineEvaluator.EvaluateAsync(
                pipelineResult: null!,
                evaluators: new[] { new FakeEvaluator() },
                chatConfiguration: config,
                cancellationToken: _ct));
    }

    [Fact]
    public async Task EvaluateAsync_NullEvaluators_ThrowsArgumentNullException()
    {
        var pipeline = new FakePipelineRunResult(new List<IAgentStageResult>());
        var config = new ChatConfiguration(new ThrowingChatClient());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PipelineEvaluator.EvaluateAsync(
                pipeline,
                evaluators: null!,
                chatConfiguration: config,
                cancellationToken: _ct));
    }

    [Fact]
    public async Task EvaluateAsync_NullChatConfiguration_ThrowsArgumentNullException()
    {
        var pipeline = new FakePipelineRunResult(new List<IAgentStageResult>());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PipelineEvaluator.EvaluateAsync(
                pipeline,
                evaluators: new[] { new FakeEvaluator() },
                chatConfiguration: null!,
                cancellationToken: _ct));
    }

    [Fact]
    public async Task EvaluateAsync_NullEvaluatorEntry_ThrowsBeforeIteratingStages()
    {
        var stageA = new FakeAgentStageResult("stage-a", FakeAgentRunDiagnostics.Create("stage-a"));
        var pipeline = new FakePipelineRunResult(new List<IAgentStageResult> { stageA });
        var config = new ChatConfiguration(new ThrowingChatClient());
        var good = new FakeEvaluator();
        var evaluators = new IEvaluator?[] { good, null };

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PipelineEvaluator.EvaluateAsync(
                pipeline,
                evaluators!,
                config,
                cancellationToken: _ct));

        Assert.Equal(0, good.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_MultipleStages_ProducesItemPerStageEvaluatorPair()
    {
        var stageA = new FakeAgentStageResult("stage-a", FakeAgentRunDiagnostics.Create("stage-a"));
        var stageB = new FakeAgentStageResult("stage-b", FakeAgentRunDiagnostics.Create("stage-b"));
        var pipeline = new FakePipelineRunResult(new List<IAgentStageResult> { stageA, stageB });
        var config = new ChatConfiguration(new ThrowingChatClient());
        var e1 = new FakeEvaluator("E1");
        var e2 = new FakeEvaluator("E2");

        var result = await PipelineEvaluator.EvaluateAsync(
            pipeline,
            new IEvaluator[] { e1, e2 },
            config,
            _ct);

        Assert.Equal(4, result.Items.Count);
        Assert.Equal(2, e1.CallCount);
        Assert.Equal(2, e2.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_StagesWithNullDiagnostics_AreSkipped()
    {
        var stageA = new FakeAgentStageResult("stage-a", FakeAgentRunDiagnostics.Create("stage-a"));
        var stageB = new FakeAgentStageResult("stage-b", diagnostics: null);
        var stageC = new FakeAgentStageResult("stage-c", FakeAgentRunDiagnostics.Create("stage-c"));
        var pipeline = new FakePipelineRunResult(new List<IAgentStageResult> { stageA, stageB, stageC });
        var config = new ChatConfiguration(new ThrowingChatClient());
        var evaluator = new FakeEvaluator();

        var result = await PipelineEvaluator.EvaluateAsync(
            pipeline,
            new[] { evaluator },
            config,
            _ct);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, evaluator.CallCount);
        Assert.All(result.Items, item => Assert.DoesNotContain("stage-b", item.Label));
    }

    [Fact]
    public async Task EvaluateAsync_LabelFormatIsAgentNameColonEvaluatorName()
    {
        var stageA = new FakeAgentStageResult("writer", FakeAgentRunDiagnostics.Create("writer"));
        var pipeline = new FakePipelineRunResult(new List<IAgentStageResult> { stageA });
        var config = new ChatConfiguration(new ThrowingChatClient());

        var result = await PipelineEvaluator.EvaluateAsync(
            pipeline,
            new[] { new FakeEvaluator() },
            config,
            _ct);

        Assert.Single(result.Items);
        Assert.Equal($"writer:{nameof(FakeEvaluator)}", result.Items[0].Label);
    }

    private sealed class FakePipelineRunResult : IPipelineRunResult
    {
        public FakePipelineRunResult(IReadOnlyList<IAgentStageResult> stages)
        {
            Stages = stages;
            FinalResponses = new Dictionary<string, ChatResponse?>();
        }

        public IReadOnlyList<IAgentStageResult> Stages { get; }
        public IReadOnlyDictionary<string, ChatResponse?> FinalResponses { get; }
        public TimeSpan TotalDuration => TimeSpan.Zero;
        public TokenUsage? AggregateTokenUsage => null;
        public bool Succeeded => true;
        public string? ErrorMessage => null;
    }

    private sealed class FakeAgentStageResult : IAgentStageResult
    {
        public FakeAgentStageResult(string agentName, IAgentRunDiagnostics? diagnostics)
        {
            AgentName = agentName;
            Diagnostics = diagnostics;
        }

        public string AgentName { get; }
        public ChatResponse? FinalResponse => null;
        public IAgentRunDiagnostics? Diagnostics { get; }
    }
}
