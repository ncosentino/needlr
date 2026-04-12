using NexusLabs.Needlr.AgentFramework.Context;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentExecutionContextExtensionsTests
{
    // -------------------------------------------------------------------------
    // GetProperty<T>() — keyed by type name
    // -------------------------------------------------------------------------

    [Fact]
    public void GetProperty_ByType_ReturnsStoredValue()
    {
        var record = new SampleRecord("hello");
        var ctx = MakeContext(typeof(SampleRecord).FullName!, record);

        var result = ctx.GetProperty<SampleRecord>();

        Assert.NotNull(result);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void GetProperty_ByType_MissingKey_ReturnsNull()
    {
        var ctx = MakeContext();

        Assert.Null(ctx.GetProperty<SampleRecord>());
    }

    [Fact]
    public void GetProperty_ByType_WrongType_ReturnsNull()
    {
        var ctx = MakeContext(typeof(SampleRecord).FullName!, "not a SampleRecord");

        Assert.Null(ctx.GetProperty<SampleRecord>());
    }

    // -------------------------------------------------------------------------
    // GetProperty<T>(key) — explicit key
    // -------------------------------------------------------------------------

    [Fact]
    public void GetProperty_ByKey_ReturnsStoredValue()
    {
        var record = new SampleRecord("world");
        var ctx = MakeContext("custom-key", record);

        var result = ctx.GetProperty<SampleRecord>("custom-key");

        Assert.NotNull(result);
        Assert.Equal("world", result.Value);
    }

    [Fact]
    public void GetProperty_ByKey_MissingKey_ReturnsNull()
    {
        var ctx = MakeContext();

        Assert.Null(ctx.GetProperty<SampleRecord>("nonexistent"));
    }

    [Fact]
    public void GetProperty_ByKey_WrongType_ReturnsNull()
    {
        var ctx = MakeContext("key", 42);

        Assert.Null(ctx.GetProperty<SampleRecord>("key"));
    }

    // -------------------------------------------------------------------------
    // Multiple properties coexist
    // -------------------------------------------------------------------------

    [Fact]
    public void GetProperty_MultipleTypes_EachResolvedIndependently()
    {
        var record1 = new SampleRecord("first");
        var record2 = new AnotherRecord(99);

        var props = new Dictionary<string, object>
        {
            [typeof(SampleRecord).FullName!] = record1,
            [typeof(AnotherRecord).FullName!] = record2,
        };
        var ctx = new AgentExecutionContext("u", "o", props);

        Assert.Equal("first", ctx.GetProperty<SampleRecord>()!.Value);
        Assert.Equal(99, ctx.GetProperty<AnotherRecord>()!.Number);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static AgentExecutionContext MakeContext(string? key = null, object? value = null)
    {
        var props = new Dictionary<string, object>();
        if (key is not null && value is not null)
        {
            props[key] = value;
        }

        return new AgentExecutionContext("user", "orch", props);
    }

    private sealed record SampleRecord(string Value);

    private sealed record AnotherRecord(int Number);
}
