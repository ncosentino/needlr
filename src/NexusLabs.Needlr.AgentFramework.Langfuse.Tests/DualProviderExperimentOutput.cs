using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// Carries subject messages, response, and BrandGhost-shaped artifacts through conformance tests.
/// </summary>
internal sealed record DualProviderExperimentOutput(
    IReadOnlyList<ChatMessage> Messages,
    ChatResponse Response,
    IReadOnlyDictionary<string, string> Artifacts);
