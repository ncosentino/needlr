using System.Text.RegularExpressions;
using NexusLabs.Needlr.AgentFramework;

namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// Terminates a workflow when an agent's response matches a specified regular expression pattern.
/// </summary>
/// <remarks>
/// Matching is performed against the full response text of each completed agent turn.
/// To restrict the condition to a specific agent, provide the agent's name or executor ID via
/// the <c>agentId</c> constructor parameter.
/// </remarks>
public sealed class RegexTerminationCondition : IWorkflowTerminationCondition
{
    private readonly Regex _regex;
    private readonly string? _agentId;

    /// <summary>
    /// Initializes a new instance that fires when <em>any</em> agent's response matches
    /// <paramref name="pattern"/> (case-insensitive, single-line).
    /// </summary>
    /// <param name="pattern">The regular expression pattern to match against response text.</param>
    public RegexTerminationCondition(string pattern)
        : this(pattern, agentId: null, RegexOptions.IgnoreCase | RegexOptions.Singleline)
    {
    }

    /// <summary>
    /// Initializes a new instance that fires when a specific agent's response matches
    /// <paramref name="pattern"/>.
    /// </summary>
    /// <param name="pattern">The regular expression pattern to match against response text.</param>
    /// <param name="agentId">
    /// The agent name or executor ID to restrict the match to, or <see langword="null"/> to
    /// match any agent.
    /// </param>
    public RegexTerminationCondition(string pattern, string? agentId)
        : this(pattern, agentId, RegexOptions.IgnoreCase | RegexOptions.Singleline)
    {
    }

    /// <summary>
    /// Initializes a new instance with full control over agent filtering and regex options.
    /// </summary>
    /// <param name="pattern">The regular expression pattern to match against response text.</param>
    /// <param name="agentId">
    /// The agent name or executor ID to restrict the match to, or <see langword="null"/> to
    /// match any agent.
    /// </param>
    /// <param name="options">Regex options applied when compiling and evaluating the pattern.</param>
    public RegexTerminationCondition(string pattern, string? agentId, RegexOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        _regex = new Regex(pattern, options, TimeSpan.FromSeconds(5));
        _agentId = agentId;
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

        return _regex.IsMatch(context.ResponseText);
    }
}
