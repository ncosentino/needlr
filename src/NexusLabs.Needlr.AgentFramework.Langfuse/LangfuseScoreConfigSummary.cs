namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>One score config entry returned by <c>GET /api/public/score-configs</c>.</summary>
internal sealed record LangfuseScoreConfigSummary
{
    /// <summary>Gets the score config name.</summary>
    public string Name { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    public IReadOnlyList<LangfuseScoreConfigCategory>? Categories { get; init; }

    public double? MinValue { get; init; }

    public double? MaxValue { get; init; }

    public string? Description { get; init; }
}
