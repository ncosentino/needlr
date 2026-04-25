using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class PipelineEvaluationExtensionsTests
{
    [Fact]
    public void ToEvaluationInputs_CollectsMessagesFromAllStages()
    {
        var stage1Messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a planner."),
            new(ChatRole.User, "Plan a trip."),
        };
        var stage2Messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a writer."),
            new(ChatRole.User, "Write the itinerary."),
        };

        var result = CreatePipelineResult(
            stages: new[]
            {
                CreateStage("planner", inputMessages: stage1Messages),
                CreateStage("writer", inputMessages: stage2Messages),
            });

        var inputs = result.ToEvaluationInputs();

        Assert.Equal(4, inputs.Messages.Count);
        Assert.Equal("You are a planner.", inputs.Messages[0].Text);
        Assert.Equal("Plan a trip.", inputs.Messages[1].Text);
        Assert.Equal("You are a writer.", inputs.Messages[2].Text);
        Assert.Equal("Write the itinerary.", inputs.Messages[3].Text);
    }

    [Fact]
    public void ToEvaluationInputs_UsesLastStageResponse()
    {
        var firstResponse = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "Draft plan"));
        var lastResponse = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "Final itinerary"));

        var result = CreatePipelineResult(
            stages: new[]
            {
                CreateStage("planner", finalResponse: firstResponse),
                CreateStage("writer", finalResponse: lastResponse),
            });

        var inputs = result.ToEvaluationInputs();

        Assert.Same(lastResponse, inputs.ModelResponse);
    }

    [Fact]
    public void ToEvaluationInputs_NoStagesWithResponse_ReturnsEmptyAssistantMessage()
    {
        var result = CreatePipelineResult(
            stages: new[]
            {
                CreateStage("agent1", finalResponse: null),
                CreateStage("agent2", finalResponse: null),
            });

        var inputs = result.ToEvaluationInputs();

        Assert.NotNull(inputs.ModelResponse);
        Assert.Single(inputs.ModelResponse.Messages);
        Assert.Equal(ChatRole.Assistant, inputs.ModelResponse.Messages[0].Role);
        Assert.Equal(string.Empty, inputs.ModelResponse.Messages[0].Text);
    }

    [Fact]
    public void ToEvaluationInputs_NullResult_Throws()
    {
        IPipelineRunResult? result = null;

        Assert.Throws<ArgumentNullException>(
            () => result!.ToEvaluationInputs());
    }

    private static FakeAgentStageResult CreateStage(
        string agentName,
        IReadOnlyList<ChatMessage>? inputMessages = null,
        ChatResponse? finalResponse = null)
    {
        return new FakeAgentStageResult
        {
            AgentName = agentName,
            FinalResponse = finalResponse,
            Diagnostics = FakeAgentRunDiagnostics.Create(
                agentName: agentName,
                inputMessages: inputMessages),
        };
    }

    private static FakePipelineRunResult CreatePipelineResult(
        IEnumerable<IAgentStageResult>? stages = null)
    {
        var stageList = stages?.ToList()
            ?? new List<IAgentStageResult>();

        return new FakePipelineRunResult
        {
            Stages = stageList,
            FinalResponses = new Dictionary<string, ChatResponse?>(),
            TotalDuration = TimeSpan.FromMilliseconds(100),
            AggregateTokenUsage = new TokenUsage(0, 0, 42, 0, 0),
            Succeeded = true,
            ErrorMessage = null,
        };
    }
}
