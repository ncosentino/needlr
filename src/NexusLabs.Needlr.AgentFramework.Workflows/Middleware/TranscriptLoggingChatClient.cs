using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Middleware;

/// <summary>
/// <see cref="DelegatingChatClient"/> middleware that intercepts
/// <see cref="GetResponseAsync"/> calls and writes request/response pairs to an
/// <see cref="ITranscriptWriter"/>.
/// </summary>
/// <remarks>
/// <para>
/// Set <see cref="CurrentStageName"/> before each pipeline stage to tag
/// transcript entries with the originating stage. If not set, entries default
/// to <c>"unknown"</c>.
/// </para>
/// <para>
/// This middleware does not buffer or transform messages — it records them
/// as-is and delegates to the inner client unchanged.
/// </para>
/// </remarks>
[DoNotAutoRegister]
public sealed class TranscriptLoggingChatClient : DelegatingChatClient
{
    private readonly ITranscriptWriter _writer;
    private string _currentStageName = "unknown";

    /// <param name="innerClient">The inner chat client to delegate to.</param>
    /// <param name="writer">The transcript writer to record entries into.</param>
    public TranscriptLoggingChatClient(
        IChatClient innerClient,
        ITranscriptWriter writer)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
    }

    /// <summary>
    /// Gets or sets the current stage name used to tag transcript entries.
    /// Call this before each stage execution.
    /// </summary>
    public string CurrentStageName
    {
        get => _currentStageName;
        set => _currentStageName = value;
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messagesList = messages as IList<ChatMessage> ?? messages.ToList();
        _writer.WriteRequest(_currentStageName, messagesList, options);

        var response = await base
            .GetResponseAsync(messagesList, options, cancellationToken)
            .ConfigureAwait(false);

        _writer.WriteResponse(_currentStageName, response);
        return response;
    }
}
