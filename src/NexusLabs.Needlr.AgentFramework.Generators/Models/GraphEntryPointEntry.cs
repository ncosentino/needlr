// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct GraphEntryPointEntry
{
    public GraphEntryPointEntry(
        string agentTypeName,
        string agentClassName,
        string graphName,
        int maxSupersteps,
        int routingMode)
    {
        AgentTypeName = agentTypeName;
        AgentClassName = agentClassName;
        GraphName = graphName;
        MaxSupersteps = maxSupersteps;
        RoutingMode = routingMode;
    }

    public string AgentTypeName { get; }
    public string AgentClassName { get; }
    public string GraphName { get; }
    public int MaxSupersteps { get; }
    public int RoutingMode { get; }
}
