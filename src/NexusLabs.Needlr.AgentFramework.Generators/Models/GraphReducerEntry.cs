// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct GraphReducerEntry
{
    public GraphReducerEntry(
        string agentTypeName,
        string agentClassName,
        string graphName,
        string reducerMethod)
    {
        AgentTypeName = agentTypeName;
        AgentClassName = agentClassName;
        GraphName = graphName;
        ReducerMethod = reducerMethod;
    }

    public string AgentTypeName { get; }
    public string AgentClassName { get; }
    public string GraphName { get; }
    public string ReducerMethod { get; }
}
