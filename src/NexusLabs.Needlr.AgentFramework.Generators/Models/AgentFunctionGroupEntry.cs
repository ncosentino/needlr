// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct AgentFunctionGroupEntry
{
    public AgentFunctionGroupEntry(string typeName, string groupName)
    {
        TypeName = typeName;
        GroupName = groupName;
    }

    public string TypeName { get; }
    public string GroupName { get; }
}
