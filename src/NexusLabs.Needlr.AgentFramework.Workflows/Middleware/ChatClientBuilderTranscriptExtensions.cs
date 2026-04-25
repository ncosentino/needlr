using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Middleware;

/// <summary>
/// Extension methods for adding transcript logging to a <see cref="ChatClientBuilder"/> pipeline.
/// </summary>
public static class ChatClientBuilderTranscriptExtensions
{
    /// <summary>
    /// Inserts a <see cref="TranscriptLoggingChatClient"/> into the pipeline that
    /// records every request/response pair to the supplied <paramref name="writer"/>.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="writer">The transcript writer to record entries into.</param>
    /// <returns>The builder, for chaining.</returns>
    public static ChatClientBuilder UseTranscriptLogging(
        this ChatClientBuilder builder,
        ITranscriptWriter writer)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(writer);

        return builder.Use(inner => new TranscriptLoggingChatClient(inner, writer));
    }
}
