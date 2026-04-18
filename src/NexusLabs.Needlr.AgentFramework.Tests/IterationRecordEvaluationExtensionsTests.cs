using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class IterationRecordEvaluationExtensionsTests
{
    private static IterationRecord MakeRecord(int iteration, params ToolCallResult[] calls) =>
        new(
            Iteration: iteration,
            ToolCalls: calls,
            FinalResponse: null,
            Tokens: new TokenUsage(0, 0, 0, 0, 0),
            Duration: TimeSpan.Zero,
            LlmCallCount: 1,
            ToolCallCount: calls.Length);

    private static ToolCallResult MakeCall(
        string name,
        IReadOnlyDictionary<string, object?>? args = null,
        object? result = null,
        bool succeeded = true,
        string? error = null) =>
        new(
            FunctionName: name,
            Arguments: args ?? new Dictionary<string, object?>(),
            Result: result ?? "ok",
            Duration: TimeSpan.Zero,
            Succeeded: succeeded,
            ErrorMessage: error);

    [Fact]
    public void ToToolCallTrajectory_NullRecord_Throws()
    {
        IterationRecord record = null!;
        Assert.Throws<ArgumentNullException>(() => record.ToToolCallTrajectory());
    }

    [Fact]
    public void ToToolCallTrajectory_NoToolCalls_ReturnsEmpty()
    {
        var record = MakeRecord(0);
        var trajectory = record.ToToolCallTrajectory();
        Assert.Empty(trajectory);
    }

    [Fact]
    public void ToToolCallTrajectory_SingleSuccessCall_EmitsAssistantThenToolMessage()
    {
        var args = new Dictionary<string, object?> { ["query"] = "weather" };
        var record = MakeRecord(3, MakeCall("Search", args, result: "sunny"));

        var trajectory = record.ToToolCallTrajectory();

        Assert.Equal(2, trajectory.Count);

        var assistant = trajectory[0];
        Assert.Equal(ChatRole.Assistant, assistant.Role);
        var call = Assert.Single(assistant.Contents.OfType<FunctionCallContent>());
        Assert.Equal("Search", call.Name);
        Assert.Equal("i3-c0", call.CallId);
        Assert.NotNull(call.Arguments);
        Assert.Equal("weather", call.Arguments!["query"]);

        var tool = trajectory[1];
        Assert.Equal(ChatRole.Tool, tool.Role);
        var result = Assert.Single(tool.Contents.OfType<FunctionResultContent>());
        Assert.Equal("i3-c0", result.CallId);
        Assert.Equal("sunny", result.Result);
    }

    [Fact]
    public void ToToolCallTrajectory_FailedCall_EmitsErrorMessageAsResult()
    {
        var record = MakeRecord(
            0,
            MakeCall("Bad", result: null, succeeded: false, error: "boom"));

        var trajectory = record.ToToolCallTrajectory();

        var result = Assert.Single(trajectory[1].Contents.OfType<FunctionResultContent>());
        Assert.Equal("boom", result.Result);
    }

    [Fact]
    public void ToToolCallTrajectory_MultipleCalls_AssignsStableUniqueCallIds()
    {
        var record = MakeRecord(
            7,
            MakeCall("A"),
            MakeCall("B"),
            MakeCall("C"));

        var trajectory = record.ToToolCallTrajectory();

        Assert.Equal(6, trajectory.Count);

        var callIds = trajectory
            .SelectMany(m => m.Contents)
            .Select(c => c switch
            {
                FunctionCallContent fc => fc.CallId,
                FunctionResultContent fr => fr.CallId,
                _ => null,
            })
            .Where(id => id is not null)
            .ToList();

        Assert.Equal(6, callIds.Count);
        Assert.Equal("i7-c0", callIds[0]);
        Assert.Equal("i7-c0", callIds[1]);
        Assert.Equal("i7-c1", callIds[2]);
        Assert.Equal("i7-c1", callIds[3]);
        Assert.Equal("i7-c2", callIds[4]);
        Assert.Equal("i7-c2", callIds[5]);
    }

    [Fact]
    public void ToToolCallTrajectory_Enumerable_NullSource_Throws()
    {
        IEnumerable<IterationRecord> records = null!;
        Assert.Throws<ArgumentNullException>(() => records.ToToolCallTrajectory());
    }

    [Fact]
    public void ToToolCallTrajectory_Enumerable_ConcatenatesInOrder()
    {
        var records = new[]
        {
            MakeRecord(0, MakeCall("A")),
            MakeRecord(1),
            MakeRecord(2, MakeCall("B"), MakeCall("C")),
        };

        var trajectory = records.ToToolCallTrajectory();

        Assert.Equal(6, trajectory.Count);

        var names = trajectory
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .Select(c => c.Name)
            .ToList();
        Assert.Equal(["A", "B", "C"], names);

        var callIds = trajectory
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .Select(c => c.CallId)
            .ToList();
        Assert.Equal(["i0-c0", "i2-c0", "i2-c1"], callIds);
    }
}
