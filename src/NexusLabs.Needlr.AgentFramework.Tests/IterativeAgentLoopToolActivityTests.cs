using System.Diagnostics;

using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

using static NexusLabs.Needlr.AgentFramework.Tests.IterativeAgentLoopToolActivityTestsHelpers;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed class IterativeAgentLoopToolActivityTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ToolCall_Success_EmitsActivitySpan()
    {
        var sourceName = $"test.tool.success.{Guid.NewGuid()}";
        using var metrics = new AgentMetrics(new AgentFrameworkMetricsOptions { MeterName = sourceName });

        var activities = new List<Activity>();
        using var listener = CreateListener(sourceName, activities);

        var mockChat = CreateToolCallThenDoneChat("GetWeather");
        var loop = CreateLoop(mockChat, metrics: metrics);
        var tool = CreateTool("GetWeather", () => "Sunny");

        await loop.RunAsync(CreateOptions([tool]), CreateContext(), _ct);

        var toolActivities = activities.Where(a => a.OperationName.StartsWith("agent.tool")).ToList();
        Assert.Single(toolActivities);
        Assert.Equal("agent.tool GetWeather", toolActivities[0].OperationName);
        Assert.Equal("GetWeather", toolActivities[0].GetTagItem("agent.tool.name"));
        Assert.Equal("success", toolActivities[0].GetTagItem("status"));
    }

    [Fact]
    public async Task ToolCall_UnknownTool_EmitsActivitySpanWithErrorStatus()
    {
        var sourceName = $"test.tool.unknown.{Guid.NewGuid()}";
        using var metrics = new AgentMetrics(new AgentFrameworkMetricsOptions { MeterName = sourceName });

        var activities = new List<Activity>();
        using var listener = CreateListener(sourceName, activities);

        var mockChat = CreateToolCallThenDoneChat("UnknownTool");
        var loop = CreateLoop(mockChat, metrics: metrics);

        await loop.RunAsync(CreateOptions([]), CreateContext(), _ct);

        var toolActivities = activities.Where(a => a.OperationName.StartsWith("agent.tool")).ToList();
        Assert.Single(toolActivities);
        Assert.Equal("agent.tool UnknownTool", toolActivities[0].OperationName);
        Assert.Equal("failed", toolActivities[0].GetTagItem("status"));
        Assert.Equal(ActivityStatusCode.Error, toolActivities[0].Status);
    }

    [Fact]
    public async Task ToolCall_Exception_EmitsActivitySpanWithErrorStatus()
    {
        var sourceName = $"test.tool.exception.{Guid.NewGuid()}";
        using var metrics = new AgentMetrics(new AgentFrameworkMetricsOptions { MeterName = sourceName });

        var activities = new List<Activity>();
        using var listener = CreateListener(sourceName, activities);

        var mockChat = CreateToolCallThenDoneChat("FailTool");
        var loop = CreateLoop(mockChat, metrics: metrics);
        var tool = CreateTool("FailTool", () => throw new InvalidOperationException("tool broke"));

        await loop.RunAsync(CreateOptions([tool]), CreateContext(), _ct);

        var toolActivities = activities.Where(a => a.OperationName.StartsWith("agent.tool")).ToList();
        Assert.Single(toolActivities);
        Assert.Equal("agent.tool FailTool", toolActivities[0].OperationName);
        Assert.Equal("failed", toolActivities[0].GetTagItem("status"));
        Assert.Equal(ActivityStatusCode.Error, toolActivities[0].Status);
    }

    [Fact]
    public async Task ToolCall_ActivityTags_IncludeSequence()
    {
        var sourceName = $"test.tool.tags.{Guid.NewGuid()}";
        using var metrics = new AgentMetrics(new AgentFrameworkMetricsOptions { MeterName = sourceName });

        var activities = new List<Activity>();
        using var listener = CreateListener(sourceName, activities);

        var mockChat = CreateToolCallThenDoneChat("ReadFile");
        var loop = CreateLoop(mockChat, metrics: metrics);
        var tool = CreateTool("ReadFile", () => "contents");

        await loop.RunAsync(CreateOptions([tool]), CreateContext(), _ct);

        var toolActivity = activities.Single(a => a.OperationName.StartsWith("agent.tool"));
        Assert.Equal(0, toolActivity.GetTagItem("agent.tool.sequence"));
    }

}
