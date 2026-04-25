using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Default implementation of <see cref="IAgentStageResult"/>.
/// </summary>
[DoNotAutoRegister]
internal sealed record AgentStageResult(
    string AgentName,
    ChatResponse? FinalResponse,
    IAgentRunDiagnostics? Diagnostics,
    StageOutcome Outcome = StageOutcome.Succeeded) : IAgentStageResult;
