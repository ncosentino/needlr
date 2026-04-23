// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System;

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct GraphReducerEntry : IEquatable<GraphReducerEntry>
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

    public bool Equals(GraphReducerEntry other) =>
        string.Equals(AgentTypeName, other.AgentTypeName, StringComparison.Ordinal) &&
        string.Equals(AgentClassName, other.AgentClassName, StringComparison.Ordinal) &&
        string.Equals(GraphName, other.GraphName, StringComparison.Ordinal) &&
        string.Equals(ReducerMethod, other.ReducerMethod, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is GraphReducerEntry other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (AgentTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (AgentClassName?.GetHashCode() ?? 0);
            hash = hash * 31 + (GraphName?.GetHashCode() ?? 0);
            hash = hash * 31 + (ReducerMethod?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public static bool operator ==(GraphReducerEntry left, GraphReducerEntry right) =>
        left.Equals(right);

    public static bool operator !=(GraphReducerEntry left, GraphReducerEntry right) =>
        !left.Equals(right);
}
