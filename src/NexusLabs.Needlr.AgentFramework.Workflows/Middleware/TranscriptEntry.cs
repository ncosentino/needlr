using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Middleware;

/// <summary>
/// A single transcript entry capturing either a request sent to the LLM or a
/// response received from it.
/// </summary>
/// <param name="StageName">The pipeline stage that produced this entry.</param>
/// <param name="Kind">Whether this entry is a request or response.</param>
/// <param name="Messages">The chat messages, present for <see cref="TranscriptEntryKind.Request"/> entries.</param>
/// <param name="Options">The chat options, present for <see cref="TranscriptEntryKind.Request"/> entries.</param>
/// <param name="Response">The chat response, present for <see cref="TranscriptEntryKind.Response"/> entries.</param>
public sealed record TranscriptEntry(
    string StageName,
    TranscriptEntryKind Kind,
    IReadOnlyList<ChatMessage>? Messages,
    ChatOptions? Options,
    ChatResponse? Response);
