namespace NexusLabs.Needlr.AgentFramework.Workflows;

/// <summary>
/// Describes a single edge in a graph workflow topology, including its
/// condition, required-ness, and any per-node routing mode override.
/// </summary>
internal sealed record GraphEdgeDetail(
    Type Source,
    Type Target,
    string? Condition,
    bool IsRequired,
    GraphRoutingMode? NodeRoutingModeOverride);
