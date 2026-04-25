using System.Diagnostics;

using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for <see cref="ChatCompletionActivityMode"/> dedup behavior in
/// <see cref="DiagnosticsChatClientMiddleware"/>.
/// </summary>
public sealed class ChatCompletionActivityModeTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static AgentMetrics CreateIsolatedMetrics([System.Runtime.CompilerServices.CallerMemberName] string? caller = null)
    {
        return new AgentMetrics(new AgentFrameworkMetricsOptions
        {
            ActivitySourceName = $"Needlr.Test.{caller}.{Guid.NewGuid():N}",
        });
    }

    [Fact]
    public async Task AlwaysMode_NoParentActivity_CreatesActivity()
    {
        using var metrics = CreateIsolatedMetrics();
        var middleware = new DiagnosticsChatClientMiddleware(
            metrics, progressAccessor: null, ChatCompletionActivityMode.Always);
        var mockInner = new Mock<IChatClient>();
        SetupSimpleResponse(mockInner);

        var activities = new List<Activity>();
        using var listener = CreateListener(metrics.ActivitySource.Name, activities);

        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: null,
            mockInner.Object,
            _ct);

        Assert.Single(activities);
        Assert.Equal("agent.chat", activities[0].OperationName);
    }

    [Fact]
    public async Task AlwaysMode_WithParentGenAiActivity_StillCreatesOwnActivity()
    {
        using var metrics = CreateIsolatedMetrics();
        var middleware = new DiagnosticsChatClientMiddleware(
            metrics, progressAccessor: null, ChatCompletionActivityMode.Always);
        var mockInner = new Mock<IChatClient>();
        SetupSimpleResponse(mockInner);

        var activities = new List<Activity>();
        using var needlrListener = CreateListener(metrics.ActivitySource.Name, activities);

        using var parentSource = new ActivitySource($"MEAI.Always.Test.{Guid.NewGuid():N}");
        using var parentListener = CreateListener(parentSource.Name);

        using var parent = parentSource.StartActivity("gen_ai.chat.completions.request");
        Assert.NotNull(parent);

        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: null,
            mockInner.Object,
            _ct);

        Assert.Single(activities);
        Assert.Equal("agent.chat", activities[0].OperationName);
    }

    [Fact]
    public async Task EnrichParentMode_NoParentActivity_CreatesActivity()
    {
        using var metrics = CreateIsolatedMetrics();
        var middleware = new DiagnosticsChatClientMiddleware(
            metrics, progressAccessor: null, ChatCompletionActivityMode.EnrichParent);
        var mockInner = new Mock<IChatClient>();
        SetupSimpleResponse(mockInner);

        var activities = new List<Activity>();
        using var listener = CreateListener(metrics.ActivitySource.Name, activities);

        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: null,
            mockInner.Object,
            _ct);

        Assert.Single(activities);
        Assert.Equal("agent.chat", activities[0].OperationName);
    }

    [Fact]
    public async Task EnrichParentMode_WithParentGenAiActivity_SuppressesOwnActivity()
    {


        using var metrics = CreateIsolatedMetrics();
        var middleware = new DiagnosticsChatClientMiddleware(
            metrics, progressAccessor: null, ChatCompletionActivityMode.EnrichParent);
        var mockInner = new Mock<IChatClient>();
        SetupSimpleResponse(mockInner);

        var activities = new List<Activity>();
        using var needlrListener = CreateListener(metrics.ActivitySource.Name, activities);

        using var parentSource = new ActivitySource($"MEAI.Enrich.Test.{Guid.NewGuid():N}");
        using var parentListener = CreateListener(parentSource.Name);

        using var parent = parentSource.StartActivity("gen_ai.chat.completions.request");
        Assert.NotNull(parent);

        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: null,
            mockInner.Object,
            _ct);

        Assert.Empty(activities);
    }

    [Fact]
    public async Task EnrichParentMode_WithParentGenAiActivity_EnrichesParentSpan()
    {


        using var metrics = CreateIsolatedMetrics();
        var middleware = new DiagnosticsChatClientMiddleware(
            metrics, progressAccessor: null, ChatCompletionActivityMode.EnrichParent);
        var mockInner = new Mock<IChatClient>();
        SetupSimpleResponse(mockInner);

        using var parentSource = new ActivitySource($"MEAI.EnrichVerify.Test.{Guid.NewGuid():N}");
        using var parentListener = CreateListener(parentSource.Name);

        using var parent = parentSource.StartActivity("gen_ai.chat.completions.request");
        Assert.NotNull(parent);

        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: null,
            mockInner.Object,
            _ct);

        var model = parent.Tags.FirstOrDefault(t => t.Key == "gen_ai.response.model").Value;
        var status = parent.Tags.FirstOrDefault(t => t.Key == "status").Value;
        Assert.Equal("test-model", model);
        Assert.Equal("success", status);
    }

    [Fact]
    public async Task EnrichParentMode_WithNonGenAiParent_CreatesActivity()
    {
        using var metrics = CreateIsolatedMetrics();
        var middleware = new DiagnosticsChatClientMiddleware(
            metrics, progressAccessor: null, ChatCompletionActivityMode.EnrichParent);
        var mockInner = new Mock<IChatClient>();
        SetupSimpleResponse(mockInner);

        var activities = new List<Activity>();
        using var needlrListener = CreateListener(metrics.ActivitySource.Name, activities);

        using var parentSource = new ActivitySource($"MyApp.NonGenAi.Test.{Guid.NewGuid():N}");
        using var parentListener = CreateListener(parentSource.Name);

        using var parent = parentSource.StartActivity("my_app.operation");
        Assert.NotNull(parent);

        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: null,
            mockInner.Object,
            _ct);

        Assert.Single(activities);
        Assert.Equal("agent.chat", activities[0].OperationName);
    }

    [Fact]
    public async Task EnrichParentMode_SuppressedActivity_StillRecordsDiagnostics()
    {
        using var metrics = CreateIsolatedMetrics();
        var middleware = new DiagnosticsChatClientMiddleware(
            metrics, progressAccessor: null, ChatCompletionActivityMode.EnrichParent);
        var mockInner = new Mock<IChatClient>();
        SetupSimpleResponse(mockInner);

        using var parentSource = new ActivitySource($"MEAI.DiagCheck.Test.{Guid.NewGuid():N}");
        using var parentListener = CreateListener(parentSource.Name);

        using var parent = parentSource.StartActivity("gen_ai.chat.completions.request");
        Assert.NotNull(parent);

        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: null,
            mockInner.Object,
            _ct);

        var diagnostics = middleware.DrainCompletions();
        Assert.Single(diagnostics);
        Assert.True(
            diagnostics[0].Succeeded,
            "Diagnostics should still be recorded even when activity is suppressed");
    }

    [Fact]
    public async Task EnrichParentMode_SuppressedActivity_StillRecordsMetrics()
    {
        using var metrics = CreateIsolatedMetrics();
        var middleware = new DiagnosticsChatClientMiddleware(
            metrics, progressAccessor: null, ChatCompletionActivityMode.EnrichParent);
        var mockInner = new Mock<IChatClient>();
        SetupSimpleResponse(mockInner);

        using var parentSource = new ActivitySource($"MEAI.MetricCheck.Test.{Guid.NewGuid():N}");
        using var parentListener = CreateListener(parentSource.Name);

        using var parent = parentSource.StartActivity("gen_ai.chat.completions.request");
        Assert.NotNull(parent);

        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: null,
            mockInner.Object,
            _ct);

        var diagnostics = middleware.DrainCompletions();
        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task EnrichParentMode_Streaming_WithParentGenAi_SuppressesActivity()
    {


        using var metrics = CreateIsolatedMetrics();
        var middleware = new DiagnosticsChatClientMiddleware(
            metrics, progressAccessor: null, ChatCompletionActivityMode.EnrichParent);

        var streamActivities = new List<Activity>();
        using var needlrListener = CreateListener(metrics.ActivitySource.Name, streamActivities);

        using var parentSource = new ActivitySource($"MEAI.Stream.Test.{Guid.NewGuid():N}");
        using var parentListener = CreateListener(parentSource.Name);

        using var parent = parentSource.StartActivity("gen_ai.chat.completions.request");
        Assert.NotNull(parent);

        var mockInner = new Mock<IChatClient>();
        var updates = new[]
        {
            new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("hi")] },
        };
        mockInner
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(updates));

        await foreach (var _ in middleware.HandleStreamingAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            options: null,
            mockInner.Object,
            _ct))
        {
        }

        var chatActivities = streamActivities
            .Where(a => a.OperationName.StartsWith("agent.chat", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(chatActivities);
    }

    private static ActivityListener CreateListener(string sourceName, List<Activity>? activities = null)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };

        if (activities is not null)
        {
            listener.ActivityStarted = a => activities.Add(a);
        }

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static void SetupSimpleResponse(Mock<IChatClient> mockInner)
    {
        mockInner
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "hello")])
            {
                ModelId = "test-model",
                Usage = new UsageDetails
                {
                    InputTokenCount = 10,
                    OutputTokenCount = 5,
                    TotalTokenCount = 15,
                },
            });
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}

