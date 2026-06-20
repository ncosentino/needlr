using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>Response shape for <c>GET /api/public/metrics</c>.</summary>
internal sealed record LangfuseMetricsResponse
{
    /// <summary>Gets the result rows (column name to value).</summary>
    public IReadOnlyList<Dictionary<string, JsonElement>> Data { get; init; } = [];
}
