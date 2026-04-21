// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct GraphEdgeEntry
{
    public GraphEdgeEntry(
        string sourceAgentTypeName,
        string sourceAgentClassName,
        string graphName,
        string targetAgentTypeName,
        string? condition,
        bool isRequired)
    {
        SourceAgentTypeName = sourceAgentTypeName;
        SourceAgentClassName = sourceAgentClassName;
        GraphName = graphName;
        TargetAgentTypeName = targetAgentTypeName;
        Condition = condition;
        IsRequired = isRequired;
    }

    public string SourceAgentTypeName { get; }
    public string SourceAgentClassName { get; }
    public string GraphName { get; }
    public string TargetAgentTypeName { get; }
    public string? Condition { get; }
    public bool IsRequired { get; }
}
