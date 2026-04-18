using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class AgentRunDiagnosticsEvaluationExtensionsTests
{
    [Fact]
    public void ToEvaluationInputs_NullDiagnostics_ThrowsArgumentNullException()
    {
        IAgentRunDiagnostics? diagnostics = null;

        Assert.Throws<ArgumentNullException>(
            () => diagnostics!.ToEvaluationInputs());
    }

    [Fact]
    public void ToEvaluationInputs_PassesInputMessagesByReference()
    {
        var inputMessages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "Hello."),
        };
        var diagnostics = CreateDiagnostics(
            inputMessages: inputMessages,
            outputResponse: null);

        var result = diagnostics.ToEvaluationInputs();

        Assert.Same(inputMessages, result.Messages);
    }

    [Fact]
    public void ToEvaluationInputs_WithOutputResponseMessages_PreservesMessageList()
    {
        var outputMessages = new List<ChatMessage>
        {
            new(ChatRole.Assistant, "Hi there!"),
        };
        var response = new AgentResponse(outputMessages);
        var diagnostics = CreateDiagnostics(
            inputMessages: new List<ChatMessage> { new(ChatRole.User, "Hello.") },
            outputResponse: response);

        var result = diagnostics.ToEvaluationInputs();

        Assert.NotNull(result.ModelResponse);
        Assert.Single(result.ModelResponse.Messages);
        Assert.Equal("Hi there!", result.ModelResponse.Messages[0].Text);
        Assert.Equal(ChatRole.Assistant, result.ModelResponse.Messages[0].Role);
    }

    [Fact]
    public void ToEvaluationInputs_NullOutputResponse_YieldsEmptyAssistantMessage()
    {
        var diagnostics = CreateDiagnostics(
            inputMessages: new List<ChatMessage> { new(ChatRole.User, "Hello.") },
            outputResponse: null);

        var result = diagnostics.ToEvaluationInputs();

        Assert.NotNull(result.ModelResponse);
        Assert.Single(result.ModelResponse.Messages);
        Assert.Equal(ChatRole.Assistant, result.ModelResponse.Messages[0].Role);
        Assert.Equal(string.Empty, result.ModelResponse.Messages[0].Text);
    }

    [Fact]
    public void ToEvaluationInputs_EmptyOutputMessages_YieldsEmptyAssistantMessage()
    {
        var response = new AgentResponse(new List<ChatMessage>());
        var diagnostics = CreateDiagnostics(
            inputMessages: new List<ChatMessage> { new(ChatRole.User, "Hello.") },
            outputResponse: response);

        var result = diagnostics.ToEvaluationInputs();

        Assert.NotNull(result.ModelResponse);
        Assert.Single(result.ModelResponse.Messages);
        Assert.Equal(ChatRole.Assistant, result.ModelResponse.Messages[0].Role);
        Assert.Equal(string.Empty, result.ModelResponse.Messages[0].Text);
    }

    private static IAgentRunDiagnostics CreateDiagnostics(
        IReadOnlyList<ChatMessage> inputMessages,
        AgentResponse? outputResponse) =>
        new FakeAgentRunDiagnostics
        {
            AgentName = "test-agent",
            TotalDuration = TimeSpan.FromMilliseconds(1),
            AggregateTokenUsage = new TokenUsage(0, 0, 0, 0, 0),
            ChatCompletions = Array.Empty<ChatCompletionDiagnostics>(),
            ToolCalls = Array.Empty<ToolCallDiagnostics>(),
            TotalInputMessages = inputMessages.Count,
            TotalOutputMessages = outputResponse?.Messages.Count ?? 0,
            InputMessages = inputMessages,
            OutputResponse = outputResponse,
            Succeeded = true,
            ErrorMessage = null,
            StartedAt = DateTimeOffset.UnixEpoch,
            CompletedAt = DateTimeOffset.UnixEpoch,
            ExecutionMode = null,
        };

    private sealed class FakeAgentRunDiagnostics : IAgentRunDiagnostics
    {
        public required string AgentName { get; init; }
        public required TimeSpan TotalDuration { get; init; }
        public required TokenUsage AggregateTokenUsage { get; init; }
        public required IReadOnlyList<ChatCompletionDiagnostics> ChatCompletions { get; init; }
        public required IReadOnlyList<ToolCallDiagnostics> ToolCalls { get; init; }
        public required int TotalInputMessages { get; init; }
        public required int TotalOutputMessages { get; init; }
        public required IReadOnlyList<ChatMessage> InputMessages { get; init; }
        public required AgentResponse? OutputResponse { get; init; }
        public required bool Succeeded { get; init; }
        public required string? ErrorMessage { get; init; }
        public required DateTimeOffset StartedAt { get; init; }
        public required DateTimeOffset CompletedAt { get; init; }
        public required string? ExecutionMode { get; init; }
    }
}
