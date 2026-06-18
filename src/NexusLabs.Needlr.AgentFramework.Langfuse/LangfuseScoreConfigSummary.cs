namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>One score config entry returned by <c>GET /api/public/score-configs</c>.</summary>
internal sealed record LangfuseScoreConfigSummary
{
    /// <summary>Gets the score config name.</summary>
    public string Name { get; init; } = string.Empty;
}
