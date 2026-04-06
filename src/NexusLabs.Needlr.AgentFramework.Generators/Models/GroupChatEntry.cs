// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct GroupChatEntry
{
    public GroupChatEntry(string agentTypeName, string groupName)
    {
        AgentTypeName = agentTypeName;
        GroupName = groupName;
    }

    public string AgentTypeName { get; }
    public string GroupName { get; }
}
