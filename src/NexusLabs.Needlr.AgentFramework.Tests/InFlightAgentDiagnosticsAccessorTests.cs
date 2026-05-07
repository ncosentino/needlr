using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed class InFlightAgentDiagnosticsAccessorTests
{
    [Fact]
    public void Current_OutsideAnyRun_ReturnsNull()
    {
        var accessor = new InFlightAgentDiagnosticsAccessor();

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Current_DuringRun_ReturnsSnapshotWithAgentName()
    {
        var accessor = new InFlightAgentDiagnosticsAccessor();
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");

        var snapshot = accessor.Current;

        Assert.NotNull(snapshot);
        Assert.Equal("Agent", snapshot!.AgentName);
        Assert.True(snapshot.Succeeded);
        Assert.Null(snapshot.ErrorMessage);
    }

    [Fact]
    public void Current_AfterBuilderDisposed_ReturnsNullAgain()
    {
        var accessor = new InFlightAgentDiagnosticsAccessor();
        var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");
        Assert.NotNull(accessor.Current);

        builder.Dispose();

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Current_ReflectsCompletedToolCallsInChronologicalOrder()
    {
        var accessor = new InFlightAgentDiagnosticsAccessor();
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");

        builder.AddToolCall(MakeToolCall(sequence: 1, name: "Second"));
        builder.AddToolCall(MakeToolCall(sequence: 0, name: "First"));

        var snapshot = accessor.Current!;

        Assert.Equal(2, snapshot.ToolCalls.Count);
        Assert.Equal("First", snapshot.ToolCalls[0].ToolName);
        Assert.Equal("Second", snapshot.ToolCalls[1].ToolName);
    }

    [Fact]
    public void Current_ReturnsImmutableSnapshot_LaterAddsDoNotMutateEarlierSnapshot()
    {
        var accessor = new InFlightAgentDiagnosticsAccessor();
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");
        builder.AddToolCall(MakeToolCall(sequence: 0, name: "First"));

        var firstSnapshot = accessor.Current!;
        Assert.Single(firstSnapshot.ToolCalls);

        builder.AddToolCall(MakeToolCall(sequence: 1, name: "Second"));

        Assert.Single(firstSnapshot.ToolCalls);
        Assert.Equal("First", firstSnapshot.ToolCalls[0].ToolName);

        var secondSnapshot = accessor.Current!;
        Assert.Equal(2, secondSnapshot.ToolCalls.Count);
    }

    [Fact]
    public void Current_ReflectsToolCallStructuredArgumentsAndResult()
    {
        var accessor = new InFlightAgentDiagnosticsAccessor();
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");

        var args = new Dictionary<string, object?> { ["issueId"] = 42 };
        var result = new { Success = false, Error = "boom" };

        builder.AddToolCall(new ToolCallDiagnostics(
            Sequence: 0,
            ToolName: "FindReplaceWithCount",
            Duration: TimeSpan.FromMilliseconds(10),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            CustomMetrics: null)
        {
            Arguments = args,
            Result = result,
        });

        var snapshot = accessor.Current!;
        var lastWrite = snapshot.ToolCalls[^1];

        Assert.Equal("FindReplaceWithCount", lastWrite.ToolName);
        Assert.NotNull(lastWrite.Arguments);
        Assert.Equal(42, lastWrite.Arguments!["issueId"]);
        Assert.Same(result, lastWrite.Result);
    }

    [Fact]
    public void Current_WithSubAgentBuilder_ReflectsInnermostRun()
    {
        var accessor = new InFlightAgentDiagnosticsAccessor();
        using var outer = AgentRunDiagnosticsBuilder.StartNew("Outer");
        outer.AddToolCall(MakeToolCall(sequence: 0, name: "OuterCall"));

        using (var inner = AgentRunDiagnosticsBuilder.StartNew("Inner"))
        {
            inner.AddToolCall(MakeToolCall(sequence: 0, name: "InnerCall"));

            var innerSnapshot = accessor.Current!;
            Assert.Equal("Inner", innerSnapshot.AgentName);
            Assert.Single(innerSnapshot.ToolCalls);
            Assert.Equal("InnerCall", innerSnapshot.ToolCalls[0].ToolName);
        }

        var outerSnapshot = accessor.Current!;
        Assert.Equal("Outer", outerSnapshot.AgentName);
        Assert.Single(outerSnapshot.ToolCalls);
        Assert.Equal("OuterCall", outerSnapshot.ToolCalls[0].ToolName);
    }

    [Fact]
    public async Task Current_OnParallelAsyncFlows_IsIsolatedPerRun()
    {
        var accessor = new InFlightAgentDiagnosticsAccessor();
        var ct = TestContext.Current.CancellationToken;
        var startBarrier = new TaskCompletionSource();
        string? agentInTask1 = null;
        string? agentInTask2 = null;
        int toolsInTask1 = -1;
        int toolsInTask2 = -1;

        var task1 = Task.Run(async () =>
        {
            using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent-A");
            builder.AddToolCall(MakeToolCall(sequence: 0, name: "A1"));
            startBarrier.TrySetResult();
            await Task.Delay(20, ct);
            var snap = accessor.Current;
            agentInTask1 = snap?.AgentName;
            toolsInTask1 = snap?.ToolCalls.Count ?? -1;
        }, ct);

        var task2 = Task.Run(async () =>
        {
            await startBarrier.Task;
            using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent-B");
            builder.AddToolCall(MakeToolCall(sequence: 0, name: "B1"));
            builder.AddToolCall(MakeToolCall(sequence: 1, name: "B2"));
            var snap = accessor.Current;
            agentInTask2 = snap?.AgentName;
            toolsInTask2 = snap?.ToolCalls.Count ?? -1;
        }, ct);

        await Task.WhenAll(task1, task2);

        Assert.Equal("Agent-A", agentInTask1);
        Assert.Equal("Agent-B", agentInTask2);
        Assert.Equal(1, toolsInTask1);
        Assert.Equal(2, toolsInTask2);
    }

    [Fact]
    public void UsingAgentFramework_RegistersInFlightAccessor()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChat = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object))
            .BuildServiceProvider(config);

        var accessor = sp.GetService<IInFlightAgentDiagnosticsAccessor>();

        Assert.NotNull(accessor);
        Assert.Null(accessor!.Current);
    }

    [Fact]
    public void AddAgentFrameworkAccessors_RegistersInFlightAccessor()
    {
        var services = new ServiceCollection();
        services.AddAgentFrameworkAccessors();

        using var sp = services.BuildServiceProvider();

        var accessor = sp.GetService<IInFlightAgentDiagnosticsAccessor>();

        Assert.NotNull(accessor);
        Assert.IsType<InFlightAgentDiagnosticsAccessor>(accessor);
    }

    [Fact]
    public void UsingAgentFramework_InFlightAccessorObservesActiveBuilder()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChat = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object))
            .BuildServiceProvider(config);

        var accessor = sp.GetRequiredService<IInFlightAgentDiagnosticsAccessor>();

        Assert.Null(accessor.Current);

        using (var builder = AgentRunDiagnosticsBuilder.StartNew("InjectedAgent"))
        {
            builder.AddToolCall(MakeToolCall(sequence: 0, name: "DiTool"));

            var snap = accessor.Current;
            Assert.NotNull(snap);
            Assert.Equal("InjectedAgent", snap!.AgentName);
            Assert.Single(snap.ToolCalls);
            Assert.Equal("DiTool", snap.ToolCalls[0].ToolName);
        }

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Current_AfterRecordFailure_ReflectsFailedSucceededFlag()
    {
        var accessor = new InFlightAgentDiagnosticsAccessor();
        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");
        builder.RecordFailure("midflight failure");

        var snap = accessor.Current!;

        Assert.False(snap.Succeeded);
        Assert.Equal("midflight failure", snap.ErrorMessage);
    }

    private static ToolCallDiagnostics MakeToolCall(int sequence, string name) =>
        new(Sequence: sequence,
            ToolName: name,
            Duration: TimeSpan.FromMilliseconds(5),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            CustomMetrics: null);
}
