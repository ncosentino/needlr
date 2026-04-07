namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Default implementation of <see cref="IAgentStageResult"/>.
/// </summary>
[DoNotAutoRegister]
internal sealed record AgentStageResult(
    string AgentName,
    string ResponseText,
    IAgentRunDiagnostics? Diagnostics) : IAgentStageResult;
