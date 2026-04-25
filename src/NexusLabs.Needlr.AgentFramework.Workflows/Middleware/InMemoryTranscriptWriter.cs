using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Middleware;

/// <summary>
/// In-memory <see cref="ITranscriptWriter"/> that stores entries in a list.
/// Useful for testing and scenarios where transcript data is consumed
/// programmatically after execution.
/// </summary>
[DoNotAutoRegister]
public sealed class InMemoryTranscriptWriter : ITranscriptWriter
{
    private readonly List<TranscriptEntry> _entries = [];

    /// <summary>
    /// Gets the transcript entries recorded so far, in order.
    /// </summary>
    public IReadOnlyList<TranscriptEntry> Entries => _entries;

    /// <inheritdoc />
    public void WriteRequest(
        string stageName,
        IEnumerable<ChatMessage> messages,
        ChatOptions? options)
    {
        _entries.Add(new TranscriptEntry(
            stageName,
            TranscriptEntryKind.Request,
            messages.ToList(),
            options,
            Response: null));
    }

    /// <inheritdoc />
    public void WriteResponse(string stageName, ChatResponse response)
    {
        _entries.Add(new TranscriptEntry(
            stageName,
            TranscriptEntryKind.Response,
            Messages: null,
            Options: null,
            response));
    }
}
