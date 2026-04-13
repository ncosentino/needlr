namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Declares that a <see cref="NeedlrAiAgentAttribute"/>-annotated agent participates in a named
/// group chat workflow. Apply this attribute to include the agent in a round-robin group chat
/// created via <see cref="IWorkflowFactory.CreateGroupChatWorkflow(string, int)"/>.
/// </summary>
/// <remarks>
/// When the <c>NexusLabs.Needlr.AgentFramework.Generators</c> package is referenced, the source
/// generator emits a strongly-typed extension method (e.g. <c>CreateCodeReviewGroupChatWorkflow</c>)
/// on <see cref="IWorkflowFactory"/> for each unique group name declared across all agent types.
/// The generated method encapsulates the group name string so the composition root never references
/// it directly.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AgentGroupChatMemberAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="AgentGroupChatMemberAttribute"/>.
    /// </summary>
    /// <param name="groupName">
    /// The name of the group chat this agent participates in. Must match exactly (case-sensitive)
    /// when calling <see cref="IWorkflowFactory.CreateGroupChatWorkflow(string, int)"/>. Use the generated
    /// extension method to avoid referencing the string directly.
    /// </param>
    public AgentGroupChatMemberAttribute(string groupName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        GroupName = groupName;
    }

    /// <summary>Gets the name of the group chat this agent participates in.</summary>
    public string GroupName { get; }

    /// <summary>
    /// Gets or sets the position of this agent in the round-robin turn order.
    /// Lower values run first. Agents with the same order are sorted alphabetically
    /// by type name for deterministic ordering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In a writer/reviewer group chat, the writer should have a lower order than
    /// the reviewer so it produces content before the reviewer evaluates it:
    /// </para>
    /// <code>
    /// [AgentGroupChatMember("article-writing", Order = 1)]
    /// public sealed class ArticleWriterAgent;
    ///
    /// [AgentGroupChatMember("article-writing", Order = 2)]
    /// public sealed class ArticleReviewerAgent;
    /// </code>
    /// <para>
    /// Defaults to <c>0</c>. When all agents have the default order, the round-robin
    /// position is determined alphabetically by type name.
    /// </para>
    /// </remarks>
    public int Order { get; set; }
}
