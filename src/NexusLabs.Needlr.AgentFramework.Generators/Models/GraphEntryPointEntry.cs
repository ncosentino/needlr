// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System;

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct GraphEntryPointEntry : IEquatable<GraphEntryPointEntry>
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

    public bool Equals(GraphEntryPointEntry other) =>
        string.Equals(AgentTypeName, other.AgentTypeName, StringComparison.Ordinal) &&
        string.Equals(AgentClassName, other.AgentClassName, StringComparison.Ordinal) &&
        string.Equals(GraphName, other.GraphName, StringComparison.Ordinal) &&
        RoutingMode == other.RoutingMode;

    public override bool Equals(object? obj) =>
        obj is GraphEntryPointEntry other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (AgentTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (AgentClassName?.GetHashCode() ?? 0);
            hash = hash * 31 + (GraphName?.GetHashCode() ?? 0);
            hash = hash * 31 + RoutingMode.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(GraphEntryPointEntry left, GraphEntryPointEntry right) =>
        left.Equals(right);

    public static bool operator !=(GraphEntryPointEntry left, GraphEntryPointEntry right) =>
        !left.Equals(right);
}
