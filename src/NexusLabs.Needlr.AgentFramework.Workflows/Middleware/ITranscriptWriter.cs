using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Middleware;

/// <summary>
/// Writes transcript entries for LLM interactions during agent execution.
/// Implementations may write to files, streams, or in-memory buffers.
/// </summary>
public interface ITranscriptWriter
{
    /// <summary>
    /// Records the messages and options sent to the LLM.
    /// </summary>
    /// <param name="stageName">The name of the pipeline stage making the request.</param>
    /// <param name="messages">The chat messages sent to the LLM.</param>
    /// <param name="options">The chat options used for the request, if any.</param>
    void WriteRequest(
        string stageName,
        IEnumerable<ChatMessage> messages,
        ChatOptions? options);

    /// <summary>
    /// Records the response received from the LLM.
    /// </summary>
    /// <param name="stageName">The name of the pipeline stage that received the response.</param>
    /// <param name="response">The chat response from the LLM.</param>
    void WriteResponse(string stageName, ChatResponse response);
}
