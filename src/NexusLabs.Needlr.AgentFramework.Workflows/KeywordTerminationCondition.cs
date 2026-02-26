using NexusLabs.Needlr.AgentFramework;

namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// Terminates a workflow when an agent's response contains a specified keyword.
/// </summary>
/// <remarks>
/// The check is a simple substring match (case-insensitive by default). To restrict the
/// condition to a specific agent, provide the agent's name or executor ID via the
/// <c>agentId</c> constructor parameter.
/// </remarks>
public sealed class KeywordTerminationCondition : IWorkflowTerminationCondition
{
    private readonly string _keyword;
    private readonly string? _agentId;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance that fires when <em>any</em> agent's response contains
    /// <paramref name="keyword"/> (case-insensitive).
    /// </summary>
    /// <param name="keyword">The keyword to look for in the response text.</param>
    public KeywordTerminationCondition(string keyword)
        : this(keyword, agentId: null, StringComparison.OrdinalIgnoreCase)
    {
    }

    /// <summary>
    /// Initializes a new instance that fires when a specific agent's response contains
    /// <paramref name="keyword"/>.
    /// </summary>
    /// <param name="keyword">The keyword to look for in the response text.</param>
    /// <param name="agentId">
    /// The agent name or executor ID to restrict the match to, or <see langword="null"/> to
    /// match any agent.
    /// </param>
    public KeywordTerminationCondition(string keyword, string? agentId)
        : this(keyword, agentId, StringComparison.OrdinalIgnoreCase)
    {
    }

    /// <summary>
    /// Initializes a new instance with full control over agent filtering and comparison.
    /// </summary>
    /// <param name="keyword">The keyword to look for in the response text.</param>
    /// <param name="agentId">
    /// The agent name or executor ID to restrict the match to, or <see langword="null"/> to
    /// match any agent.
    /// </param>
    /// <param name="comparison">The string comparison used when searching for the keyword.</param>
    public KeywordTerminationCondition(string keyword, string? agentId, StringComparison comparison)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);
        _keyword = keyword;
        _agentId = agentId;
        _comparison = comparison;
    }

    /// <inheritdoc/>
    public bool ShouldTerminate(TerminationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_agentId is not null
            && !string.Equals(context.AgentId, _agentId, StringComparison.Ordinal)
            && !context.AgentId.StartsWith(_agentId + "_", StringComparison.Ordinal))
        {
            return false;
        }

        return context.ResponseText.Contains(_keyword, _comparison);
    }
}
