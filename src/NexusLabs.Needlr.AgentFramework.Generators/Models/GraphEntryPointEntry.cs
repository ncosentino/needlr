// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct GraphEntryPointEntry
{
    public GraphEntryPointEntry(
        string agentTypeName,
        string agentClassName,
        string graphName,
        int routingMode)
    {
        AgentTypeName = agentTypeName;
        AgentClassName = agentClassName;
        GraphName = graphName;
        RoutingMode = routingMode;
    }

    public string AgentTypeName { get; }
    public string AgentClassName { get; }
    public string GraphName { get; }
    public int RoutingMode { get; }
}
