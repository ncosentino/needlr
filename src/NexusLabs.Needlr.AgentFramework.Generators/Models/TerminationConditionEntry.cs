// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct TerminationConditionEntry
{
    public TerminationConditionEntry(string agentTypeName, string conditionTypeFQN, ImmutableArray<string> ctorArgLiterals)
    {
        AgentTypeName = agentTypeName;
        ConditionTypeFQN = conditionTypeFQN;
        CtorArgLiterals = ctorArgLiterals;
    }

    public string AgentTypeName { get; }
    public string ConditionTypeFQN { get; }
    public ImmutableArray<string> CtorArgLiterals { get; }
}
