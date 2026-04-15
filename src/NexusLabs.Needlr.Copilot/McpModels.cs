using System.Text.Json.Serialization;

namespace NexusLabs.Needlr.Copilot;

// ── MCP JSON-RPC DTOs ──

[JsonSerializable(typeof(McpJsonRpcRequest))]
[JsonSerializable(typeof(McpJsonRpcResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class McpJsonContext : JsonSerializerContext;

internal sealed class McpJsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    public McpCallParams? Params { get; set; }
}

internal sealed class McpCallParams
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("arguments")]
    public Dictionary<string, string>? Arguments { get; set; }
}

internal sealed class McpJsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string? Jsonrpc { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("result")]
    public McpResult? Result { get; set; }

    [JsonPropertyName("error")]
    public McpError? Error { get; set; }
}

internal sealed class McpResult
{
    [JsonPropertyName("content")]
    public List<McpContent>? Content { get; set; }
}

internal sealed class McpContent
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

internal sealed class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
