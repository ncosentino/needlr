// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct GraphNodeEntry
{
    public GraphNodeEntry(
        string agentTypeName,
        string agentClassName,
        string graphName,
        int joinMode)
    {
        AgentTypeName = agentTypeName;
        AgentClassName = agentClassName;
        GraphName = graphName;
        JoinMode = joinMode;
    }

    public string AgentTypeName { get; }
    public string AgentClassName { get; }
    public string GraphName { get; }
    public int JoinMode { get; }
}
