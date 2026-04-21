using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class TaskCompletionEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task EvaluateAsync_NullChatConfiguration_ReturnsEmptyResult()
    {
        var evaluator = new TaskCompletionEvaluator();

        var result = await evaluator.EvaluateAsync(
            messages: [new ChatMessage(ChatRole.User, "Do something")],
            modelResponse: new ChatResponse(new ChatMessage(ChatRole.Assistant, "Done")),
            chatConfiguration: null,
            cancellationToken: _ct);

        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task EvaluateAsync_JudgeSaysCompleted_ReturnsCorrectMetrics()
    {
        var judgeResponse = """
            SCORE: 5
            COMPLETED: YES
            REASONING: The agent fully accomplished the requested task with comprehensive output.
            """;
        using var judge = new RecordingChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, judgeResponse)));
        var evaluator = new TaskCompletionEvaluator();

        var result = await evaluator.EvaluateAsync(
            messages: [new ChatMessage(ChatRole.User, "Plan a trip")],
            modelResponse: new ChatResponse(new ChatMessage(ChatRole.Assistant, "Here is your trip...")),
            chatConfiguration: new ChatConfiguration(judge),
            cancellationToken: _ct);

        Assert.Equal(3, result.Metrics.Count);

        var completed = Assert.IsType<BooleanMetric>(
            result.Metrics[TaskCompletionEvaluator.TaskCompletedMetricName]);
        Assert.True(completed.Value, "Expected task to be marked as completed");

        var score = Assert.IsType<NumericMetric>(
            result.Metrics[TaskCompletionEvaluator.TaskCompletionScoreMetricName]);
        Assert.Equal(5, score.Value);

        var reasoning = Assert.IsType<StringMetric>(
            result.Metrics[TaskCompletionEvaluator.TaskCompletionReasoningMetricName]);
        Assert.Contains("fully accomplished", reasoning.Value);
    }

    [Fact]
    public async Task EvaluateAsync_JudgeSaysNotCompleted_ReturnsCorrectMetrics()
    {
        var judgeResponse = """
            SCORE: 1
            COMPLETED: NO
            REASONING: The agent produced no relevant output for the requested task.
            """;
        using var judge = new RecordingChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, judgeResponse)));
        var evaluator = new TaskCompletionEvaluator();

        var result = await evaluator.EvaluateAsync(
            messages: [new ChatMessage(ChatRole.User, "Write a poem")],
            modelResponse: new ChatResponse(new ChatMessage(ChatRole.Assistant, "Error occurred")),
            chatConfiguration: new ChatConfiguration(judge),
            cancellationToken: _ct);

        var completed = Assert.IsType<BooleanMetric>(
            result.Metrics[TaskCompletionEvaluator.TaskCompletedMetricName]);
        Assert.False(completed.Value, "Expected task to be marked as not completed");

        var score = Assert.IsType<NumericMetric>(
            result.Metrics[TaskCompletionEvaluator.TaskCompletionScoreMetricName]);
        Assert.Equal(1, score.Value);
    }

    [Fact]
    public async Task EvaluateAsync_WithDiagnosticsContext_IncludesToolCallInfo()
    {
        var judgeResponse = """
            SCORE: 4
            COMPLETED: YES
            REASONING: Task mostly completed.
            """;
        using var judge = new RecordingChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, judgeResponse)));
        var evaluator = new TaskCompletionEvaluator();

        var diagnostics = FakeAgentRunDiagnostics.Create();

        var result = await evaluator.EvaluateAsync(
            messages: [new ChatMessage(ChatRole.User, "Search and summarize")],
            modelResponse: new ChatResponse(new ChatMessage(ChatRole.Assistant, "Summary")),
            chatConfiguration: new ChatConfiguration(judge),
            additionalContext: [new AgentRunDiagnosticsContext(diagnostics)],
            cancellationToken: _ct);

        Assert.Equal(3, result.Metrics.Count);
        Assert.Equal(1, judge.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_EmptyAgentOutput_JudgeStillCalled()
    {
        var judgeResponse = """
            SCORE: 1
            COMPLETED: NO
            REASONING: Agent produced empty output.
            """;
        using var judge = new RecordingChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, judgeResponse)));
        var evaluator = new TaskCompletionEvaluator();

        var result = await evaluator.EvaluateAsync(
            messages: [new ChatMessage(ChatRole.User, "Do something")],
            modelResponse: new ChatResponse(),
            chatConfiguration: new ChatConfiguration(judge),
            cancellationToken: _ct);

        Assert.Equal(1, judge.CallCount);
        var score = Assert.IsType<NumericMetric>(
            result.Metrics[TaskCompletionEvaluator.TaskCompletionScoreMetricName]);
        Assert.Equal(1, score.Value);
    }

    [Fact]
    public void ParseJudgeResponse_WellFormatted_ParsesCorrectly()
    {
        var response = """
            SCORE: 4
            COMPLETED: YES
            REASONING: The agent did a good job overall.
            """;

        var (score, completed, reasoning) =
            TaskCompletionEvaluator.ParseJudgeResponse(response);

        Assert.Equal(4, score);
        Assert.True(completed, "Expected completed to be true");
        Assert.Contains("good job", reasoning);
    }

    [Fact]
    public void ParseJudgeResponse_ScoreOutOfRange_ClampedTo5()
    {
        var response = "SCORE: 9\nCOMPLETED: YES\nREASONING: Perfect.";

        var (score, _, _) = TaskCompletionEvaluator.ParseJudgeResponse(response);

        Assert.Equal(5, score);
    }

    [Fact]
    public void ParseJudgeResponse_ScoreZero_ClampedTo1()
    {
        var response = "SCORE: 0\nCOMPLETED: NO\nREASONING: Terrible.";

        var (score, _, _) = TaskCompletionEvaluator.ParseJudgeResponse(response);

        Assert.Equal(1, score);
    }

    [Fact]
    public void ParseJudgeResponse_MissingCompleted_InferredFromScore()
    {
        var response = "SCORE: 4\nREASONING: Good work.";

        var (score, completed, _) = TaskCompletionEvaluator.ParseJudgeResponse(response);

        Assert.Equal(4, score);
        Assert.True(completed, "Expected completed to be inferred as true from score >= 3");
    }

    [Fact]
    public void ParseJudgeResponse_MissingCompletedLowScore_InferredFalse()
    {
        var response = "SCORE: 2\nREASONING: Not great.";

        var (_, completed, _) = TaskCompletionEvaluator.ParseJudgeResponse(response);

        Assert.False(completed, "Expected completed to be inferred as false from score < 3");
    }

    [Fact]
    public void ParseJudgeResponse_GarbageResponse_DefaultsToScore1()
    {
        var response = "I don't understand the question.";

        var (score, completed, reasoning) =
            TaskCompletionEvaluator.ParseJudgeResponse(response);

        Assert.Equal(1, score);
        Assert.False(completed, "Expected completed to be false for garbage response");
        Assert.Equal("Unable to parse judge response.", reasoning);
    }

    [Fact]
    public void ParseJudgeResponse_CaseInsensitive_ParsesCorrectly()
    {
        var response = "score: 3\ncompleted: yes\nreasoning: Adequate.";

        var (score, completed, reasoning) =
            TaskCompletionEvaluator.ParseJudgeResponse(response);

        Assert.Equal(3, score);
        Assert.True(completed, "Expected case-insensitive parsing to work");
        Assert.Equal("Adequate.", reasoning);
    }

    [Fact]
    public void ParseJudgeResponse_MultilineReasoning_CapturesAll()
    {
        var response = """
            SCORE: 5
            COMPLETED: YES
            REASONING: The agent did everything asked. It searched for flights, compared prices, and built a complete itinerary with hotels and activities.
            """;

        var (_, _, reasoning) = TaskCompletionEvaluator.ParseJudgeResponse(response);

        Assert.Contains("searched for flights", reasoning);
        Assert.Contains("complete itinerary", reasoning);
    }
}
