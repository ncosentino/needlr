using NexusLabs.Needlr.AgentFramework;

namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// Terminates a workflow when an agent calls a specific tool/function during its turn.
/// Unlike <see cref="KeywordTerminationCondition"/> which matches response text,
/// this condition matches on structured tool call data — eliminating false positives
/// from keywords appearing in natural language.
/// </summary>
/// <remarks>
/// <para>
/// This condition inspects <see cref="TerminationContext.ToolCallNames"/> which is
/// populated from <c>FunctionCallContent</c> entries in the agent's response message.
/// The match is exact and case-sensitive on the tool name.
/// </para>
/// <para>
/// Designed for scenarios where an agent should signal approval or completion via a
/// dedicated tool call rather than embedding a keyword in free text. For example, a
/// reviewer agent calls <c>ApproveArticle()</c> instead of writing "APPROVED" — which
/// avoids the problem of the LLM accidentally including the keyword in rejection text.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Declare on the reviewer agent class:
/// [AgentGroupChatMember("article-writing")]
/// [AgentTerminationCondition(typeof(ToolCallTerminationCondition), "ApproveArticle", "ReviewerAgent")]
/// public sealed class ReviewerAgent;
///
/// // The reviewer's instructions tell it to call ApproveArticle() when satisfied:
/// // "If no Critical or Major issues remain, call the ApproveArticle tool."
/// </code>
/// </example>
public sealed class ToolCallTerminationCondition : IWorkflowTerminationCondition
{
    private readonly string _toolName;
    private readonly string? _agentId;

    /// <summary>
    /// Initializes a new instance that fires when <em>any</em> agent calls the
    /// specified tool.
    /// </summary>
    /// <param name="toolName">The exact tool/function name to match (case-sensitive).</param>
    public ToolCallTerminationCondition(string toolName)
        : this(toolName, agentId: null)
    {
    }

    /// <summary>
    /// Initializes a new instance that fires when a specific agent calls the
    /// specified tool.
    /// </summary>
    /// <param name="toolName">The exact tool/function name to match (case-sensitive).</param>
    /// <param name="agentId">
    /// The agent name or executor ID to restrict the match to, or <see langword="null"/>
    /// to match any agent. Supports MAF's GUID-suffixed executor IDs via prefix matching.
    /// </param>
    public ToolCallTerminationCondition(string toolName, string? agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        _toolName = toolName;
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

        foreach (var toolName in context.ToolCallNames)
        {
            if (string.Equals(toolName, _toolName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
