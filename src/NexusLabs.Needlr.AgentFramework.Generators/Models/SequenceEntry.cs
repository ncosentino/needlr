// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct SequenceEntry
{
    public SequenceEntry(string agentTypeName, string pipelineName, int order)
    {
        AgentTypeName = agentTypeName;
        PipelineName = pipelineName;
        Order = order;
    }

    public string AgentTypeName { get; }
    public string PipelineName { get; }
    public int Order { get; }
}
