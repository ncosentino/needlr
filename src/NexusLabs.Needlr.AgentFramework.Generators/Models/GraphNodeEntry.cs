// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System;

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct GraphNodeEntry : IEquatable<GraphNodeEntry>
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

    public bool Equals(GraphNodeEntry other) =>
        string.Equals(AgentTypeName, other.AgentTypeName, StringComparison.Ordinal) &&
        string.Equals(AgentClassName, other.AgentClassName, StringComparison.Ordinal) &&
        string.Equals(GraphName, other.GraphName, StringComparison.Ordinal) &&
        JoinMode == other.JoinMode;

    public override bool Equals(object? obj) =>
        obj is GraphNodeEntry other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (AgentTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (AgentClassName?.GetHashCode() ?? 0);
            hash = hash * 31 + (GraphName?.GetHashCode() ?? 0);
            hash = hash * 31 + JoinMode.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(GraphNodeEntry left, GraphNodeEntry right) =>
        left.Equals(right);

    public static bool operator !=(GraphNodeEntry left, GraphNodeEntry right) =>
        !left.Equals(right);
}
