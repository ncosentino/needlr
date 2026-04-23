// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System;

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct GraphEdgeEntry : IEquatable<GraphEdgeEntry>
{
    public GraphEdgeEntry(
        string sourceAgentTypeName,
        string sourceAgentClassName,
        string graphName,
        string targetAgentTypeName,
        string? condition,
        bool isRequired,
        int? nodeRoutingMode)
    {
        SourceAgentTypeName = sourceAgentTypeName;
        SourceAgentClassName = sourceAgentClassName;
        GraphName = graphName;
        TargetAgentTypeName = targetAgentTypeName;
        Condition = condition;
        IsRequired = isRequired;
        NodeRoutingMode = nodeRoutingMode;
    }

    public string SourceAgentTypeName { get; }
    public string SourceAgentClassName { get; }
    public string GraphName { get; }
    public string TargetAgentTypeName { get; }
    public string? Condition { get; }
    public bool IsRequired { get; }
    public int? NodeRoutingMode { get; }

    public bool Equals(GraphEdgeEntry other) =>
        string.Equals(SourceAgentTypeName, other.SourceAgentTypeName, StringComparison.Ordinal) &&
        string.Equals(SourceAgentClassName, other.SourceAgentClassName, StringComparison.Ordinal) &&
        string.Equals(GraphName, other.GraphName, StringComparison.Ordinal) &&
        string.Equals(TargetAgentTypeName, other.TargetAgentTypeName, StringComparison.Ordinal) &&
        string.Equals(Condition, other.Condition, StringComparison.Ordinal) &&
        IsRequired == other.IsRequired &&
        NodeRoutingMode == other.NodeRoutingMode;

    public override bool Equals(object? obj) =>
        obj is GraphEdgeEntry other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (SourceAgentTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (SourceAgentClassName?.GetHashCode() ?? 0);
            hash = hash * 31 + (GraphName?.GetHashCode() ?? 0);
            hash = hash * 31 + (TargetAgentTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (Condition?.GetHashCode() ?? 0);
            hash = hash * 31 + IsRequired.GetHashCode();
            hash = hash * 31 + NodeRoutingMode.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(GraphEdgeEntry left, GraphEdgeEntry right) =>
        left.Equals(right);

    public static bool operator !=(GraphEdgeEntry left, GraphEdgeEntry right) =>
        !left.Equals(right);
}
