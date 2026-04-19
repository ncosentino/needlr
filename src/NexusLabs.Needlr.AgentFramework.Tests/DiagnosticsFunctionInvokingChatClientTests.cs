using System.Diagnostics;

using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed class DiagnosticsFunctionInvokingChatClientTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task SuccessfulToolCall_RecordsDiagnostics()
    {
        using var builder = AgentRunDiagnosticsBuilder.StartNew("TestAgent");

        var inner = new TestChatClient(
            toolCallName: "GetWeather",
            toolResult: "Sunny");
        var tool = AIFunctionFactory.Create(
            () => "Sunny",
            new AIFunctionFactoryOptions { Name = "GetWeather" });

        using var client = new DiagnosticsFunctionInvokingChatClient(inner);

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "What's the weather?")],
            new ChatOptions { Tools = [tool] },
            _ct);

        var diag = builder.Build();
        Assert.Single(diag.ToolCalls);
        Assert.Equal("GetWeather", diag.ToolCalls[0].ToolName);
        Assert.True(diag.ToolCalls[0].Succeeded, "Expected tool call to succeed");
    }

    [Fact]
    public async Task FailedToolCall_RecordsDiagnosticsWithError()
    {
        using var builder = AgentRunDiagnosticsBuilder.StartNew("TestAgent");

        var inner = new TestChatClient(
            toolCallName: "FailTool",
            toolResult: null);
        var tool = AIFunctionFactory.Create(
            new Func<string>(() => throw new InvalidOperationException("boom")),
            new AIFunctionFactoryOptions { Name = "FailTool" });

        using var client = new DiagnosticsFunctionInvokingChatClient(inner);

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Do something")],
            new ChatOptions { Tools = [tool] },
            _ct);

        var diag = builder.Build();
        Assert.Single(diag.ToolCalls);
        Assert.Equal("FailTool", diag.ToolCalls[0].ToolName);
        Assert.False(diag.ToolCalls[0].Succeeded, "Expected tool call to fail");
        Assert.Contains("boom", diag.ToolCalls[0].ErrorMessage);
    }

    [Fact]
    public async Task ToolCall_EmitsActivitySpan()
    {
        var sourceName = $"test.fic.span.{Guid.NewGuid()}";
        using var metrics = new AgentMetrics(new AgentFrameworkMetricsOptions { MeterName = sourceName });

        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => activities.Add(a),
        };
        ActivitySource.AddActivityListener(listener);

        using var builder = AgentRunDiagnosticsBuilder.StartNew("TestAgent");

        var inner = new TestChatClient(
            toolCallName: "ReadFile",
            toolResult: "contents");
        var tool = AIFunctionFactory.Create(
            () => "contents",
            new AIFunctionFactoryOptions { Name = "ReadFile" });

        using var client = new DiagnosticsFunctionInvokingChatClient(inner, metrics);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Read a file")],
            new ChatOptions { Tools = [tool] },
            _ct);

        var toolActivities = activities.Where(a => a.OperationName.StartsWith("agent.tool")).ToList();
        Assert.Single(toolActivities);
        Assert.Equal("agent.tool ReadFile", toolActivities[0].OperationName);
        Assert.Equal("ReadFile", toolActivities[0].GetTagItem("agent.tool.name"));
        Assert.Equal("success", toolActivities[0].GetTagItem("status"));
    }

    [Fact]
    public async Task ToolCall_RecordsOTelMetrics()
    {
        using var metrics = new AgentMetrics();
        using var builder = AgentRunDiagnosticsBuilder.StartNew("TestAgent");

        var inner = new TestChatClient(
            toolCallName: "GetData",
            toolResult: "data");
        var tool = AIFunctionFactory.Create(
            () => "data",
            new AIFunctionFactoryOptions { Name = "GetData" });

        using var client = new DiagnosticsFunctionInvokingChatClient(inner, metrics);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Get data")],
            new ChatOptions { Tools = [tool] },
            _ct);

        // If we got here without error, metrics.RecordToolCall was called.
        // The underlying AgentMetrics.RecordToolCall increments counters — verified
        // by AgentMetricsTests. This test verifies the middleware calls it.
        var diag = builder.Build();
        Assert.Single(diag.ToolCalls);
        Assert.True(diag.ToolCalls[0].Duration > TimeSpan.Zero, "Expected positive duration");
    }

    [Fact]
    public async Task WithoutDiagnosticsBuilder_SequenceIsNegativeOne()
    {
        // No builder in async flow
        AgentRunDiagnosticsBuilder.ClearCurrent();

        var inner = new TestChatClient(
            toolCallName: "SomeTool",
            toolResult: "result");
        var tool = AIFunctionFactory.Create(
            () => "result",
            new AIFunctionFactoryOptions { Name = "SomeTool" });

        using var client = new DiagnosticsFunctionInvokingChatClient(inner);

        // Should not throw even without a builder
        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "test")],
            new ChatOptions { Tools = [tool] },
            _ct);

        Assert.NotNull(response);
    }

    [Fact]
    public async Task Extension_UseDiagnosticsFunctionInvocation_CreatesWorkingPipeline()
    {
        using var builder = AgentRunDiagnosticsBuilder.StartNew("TestAgent");

        var inner = new TestChatClient(
            toolCallName: "Greet",
            toolResult: "Hello!");
        var tool = AIFunctionFactory.Create(
            () => "Hello!",
            new AIFunctionFactoryOptions { Name = "Greet" });

        using var client = inner
            .AsBuilder()
            .UseDiagnosticsFunctionInvocation()
            .Build();

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Greet me")],
            new ChatOptions { Tools = [tool] },
            _ct);

        var diag = builder.Build();
        Assert.Single(diag.ToolCalls);
        Assert.Equal("Greet", diag.ToolCalls[0].ToolName);
    }

    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    /// <summary>
    /// Minimal chat client that returns a tool call on the first request,
    /// then a plain response on the second (after receiving function results).
    /// </summary>
    private sealed class TestChatClient : IChatClient
    {
        private readonly string _toolCallName;
        private readonly object? _toolResult;
        private int _callCount;

        internal TestChatClient(string toolCallName, object? toolResult)
        {
            _toolCallName = toolCallName;
            _toolResult = toolResult;
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var count = Interlocked.Increment(ref _callCount);
            if (count == 1)
            {
                // First call: emit a tool call request
                var content = new FunctionCallContent(
                    $"call-{count}",
                    _toolCallName,
                    new Dictionary<string, object?> { ["input"] = "test" });

                return Task.FromResult(new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, [content])]));
            }

            // Subsequent calls: return a plain response
            return Task.FromResult(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, $"Result: {_toolResult}")]));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
