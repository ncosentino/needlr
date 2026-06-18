namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Serializable payload for <c>POST /api/public/comments</c>. Property names are projected to
/// camelCase by <see cref="LangfuseApiClient"/>.
/// </summary>
internal sealed record LangfuseCommentRequest
{
    /// <summary>Gets the id of the project the comment is attached to. Required by Langfuse.</summary>
    public required string ProjectId { get; init; }

    /// <summary>Gets the object type (<c>TRACE</c>, <c>OBSERVATION</c>, <c>SESSION</c>, <c>PROMPT</c>).</summary>
    public required string ObjectType { get; init; }

    /// <summary>Gets the id of the object the comment is attached to.</summary>
    public required string ObjectId { get; init; }

    /// <summary>Gets the comment content (markdown; Langfuse limits this to 5000 characters).</summary>
    public required string Content { get; init; }

    /// <summary>Gets the optional id of the user who authored the comment.</summary>
    public string? AuthorUserId { get; init; }
}
