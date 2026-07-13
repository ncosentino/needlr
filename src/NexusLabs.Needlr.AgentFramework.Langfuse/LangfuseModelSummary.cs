namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>One model entry returned by <c>GET /api/public/models</c>.</summary>
internal sealed record LangfuseModelSummary
{
    /// <summary>Gets the model definition name.</summary>
    public string ModelName { get; init; } = string.Empty;

    public string MatchPattern { get; init; } = string.Empty;

    public string Unit { get; init; } = string.Empty;

    public double? InputPrice { get; init; }

    public double? OutputPrice { get; init; }

    public double? TotalPrice { get; init; }
}
