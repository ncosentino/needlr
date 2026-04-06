// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct HandoffEntry
{
    public HandoffEntry(string initialAgentTypeName, string initialAgentClassName, string targetAgentTypeName, string? handoffReason)
    {
        InitialAgentTypeName = initialAgentTypeName;
        InitialAgentClassName = initialAgentClassName;
        TargetAgentTypeName = targetAgentTypeName;
        HandoffReason = handoffReason;
    }

    public string InitialAgentTypeName { get; }
    public string InitialAgentClassName { get; }
    public string TargetAgentTypeName { get; }
    public string? HandoffReason { get; }
}
