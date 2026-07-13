namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// Describes one HTTP request captured by a test boundary handler.
/// </summary>
internal sealed record CapturedRequest(
    HttpMethod Method,
    Uri Uri,
    string? Body);
