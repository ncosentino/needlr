using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Default implementation of <see cref="IDagNodeResult"/>.
/// </summary>
[DoNotAutoRegister]
internal sealed class DagNodeResult : IDagNodeResult
{
    internal DagNodeResult(
        string nodeId,
        string agentName,
        NodeKind kind,
        IAgentRunDiagnostics? diagnostics,
        ChatResponse? finalResponse,
        IReadOnlyList<string> inboundEdges,
        IReadOnlyList<string> outboundEdges,
        TimeSpan startOffset,
        TimeSpan duration)
    {
        NodeId = nodeId;
        AgentName = agentName;
        Kind = kind;
        Diagnostics = diagnostics;
        FinalResponse = finalResponse;
        InboundEdges = inboundEdges;
        OutboundEdges = outboundEdges;
        StartOffset = startOffset;
        Duration = duration;
    }

    public string NodeId { get; }

    public string AgentName { get; }

    public NodeKind Kind { get; }

    public IAgentRunDiagnostics? Diagnostics { get; }

    public ChatResponse? FinalResponse { get; }

    public IReadOnlyList<string> InboundEdges { get; }

    public IReadOnlyList<string> OutboundEdges { get; }

    public TimeSpan StartOffset { get; }

    public TimeSpan Duration { get; }
}
