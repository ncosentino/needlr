using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for <see cref="ToolResultSerializer"/> which is the shared utility
/// used by IterativeAgentLoop, IterationRecordEvaluationExtensions,
/// ContextWindowGuardMiddleware, and example apps.
/// </summary>
public sealed class ToolResultSerializerTests
{
    [Fact]
    public void Serialize_Null_ReturnsEmptyString()
    {
        Assert.Equal("", ToolResultSerializer.Serialize(null));
    }

    [Fact]
    public void Serialize_String_ReturnsStringAsIs()
    {
        Assert.Equal("hello world", ToolResultSerializer.Serialize("hello world"));
    }

    [Fact]
    public void Serialize_EmptyString_ReturnsEmptyString()
    {
        Assert.Equal("", ToolResultSerializer.Serialize(""));
    }

    [Fact]
    public void Serialize_JsonElementObject_ReturnsRawJson()
    {
        using var doc = JsonDocument.Parse("""{"Title":"Node.js","Url":"https://nodejs.org"}""");
        var element = doc.RootElement.Clone();

        var result = ToolResultSerializer.Serialize(element);

        Assert.Contains("Node.js", result);
        Assert.Contains("https://nodejs.org", result);

        using var parsed = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Object, parsed.RootElement.ValueKind);
    }

    [Fact]
    public void Serialize_JsonElementArray_ReturnsRawJsonArray()
    {
        using var doc = JsonDocument.Parse("""[{"Title":"A"},{"Title":"B"}]""");
        var element = doc.RootElement.Clone();

        var result = ToolResultSerializer.Serialize(element);

        Assert.StartsWith("[", result);
        Assert.Contains("\"A\"", result);
        Assert.Contains("\"B\"", result);
    }

    [Fact]
    public void Serialize_JsonElementString_ReturnsQuotedString()
    {
        using var doc = JsonDocument.Parse("\"hello\"");
        var element = doc.RootElement.Clone();

        var result = ToolResultSerializer.Serialize(element);

        Assert.Equal("\"hello\"", result);
    }

    [Fact]
    public void Serialize_JsonElementNull_ReturnsNullLiteral()
    {
        using var doc = JsonDocument.Parse("null");
        var element = doc.RootElement.Clone();

        var result = ToolResultSerializer.Serialize(element);

        Assert.Equal("null", result);
    }

    [Fact]
    public void Serialize_JsonElementNumber_ReturnsNumber()
    {
        using var doc = JsonDocument.Parse("42");
        var element = doc.RootElement.Clone();

        var result = ToolResultSerializer.Serialize(element);

        Assert.Equal("42", result);
    }

    [Fact]
    public void Serialize_PlainObject_ReturnsJsonSerialized()
    {
        var obj = new TestRecord("Node.js", "https://nodejs.org");

        var result = ToolResultSerializer.Serialize(obj);

        Assert.Contains("Node.js", result);
        Assert.Contains("https://nodejs.org", result);
        Assert.DoesNotContain("TestRecord", result);

        using var parsed = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Object, parsed.RootElement.ValueKind);
    }

    [Fact]
    public void Serialize_Array_ReturnsJsonArray()
    {
        var arr = new[]
        {
            new TestRecord("A", "https://a.com"),
            new TestRecord("B", "https://b.com"),
        };

        var result = ToolResultSerializer.Serialize(arr);

        Assert.StartsWith("[", result);
        Assert.Contains("https://a.com", result);
        Assert.DoesNotContain("TestRecord[]", result);
    }

    [Fact]
    public void Serialize_Integer_ReturnsSerializedValue()
    {
        Assert.Equal("42", ToolResultSerializer.Serialize(42));
    }

    [Fact]
    public void Serialize_Boolean_ReturnsSerializedValue()
    {
        Assert.Equal("true", ToolResultSerializer.Serialize(true));
    }

    [Fact]
    public void Serialize_ObjectWithCustomToString_ReturnsJsonNotToString()
    {
        var obj = new CustomToStringObject { Value = 99, Name = "test" };

        var result = ToolResultSerializer.Serialize(obj);

        Assert.DoesNotContain("CUSTOM_OUTPUT", result);
        Assert.Contains("99", result);
        Assert.Contains("test", result);
    }

    private sealed record TestRecord(string Title, string Url);

    private sealed class CustomToStringObject
    {
        public int Value { get; set; }
        public string Name { get; set; } = "";
        public override string ToString() => "CUSTOM_OUTPUT";
    }
}
