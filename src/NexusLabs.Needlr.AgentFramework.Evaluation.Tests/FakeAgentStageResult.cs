using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

internal sealed class FakeAgentStageResult : IAgentStageResult
{
    public required string AgentName { get; init; }

    public required ChatResponse? FinalResponse { get; init; }

    public required IAgentRunDiagnostics? Diagnostics { get; init; }
}
