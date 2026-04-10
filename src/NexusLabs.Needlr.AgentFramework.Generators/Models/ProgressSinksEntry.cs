// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace NexusLabs.Needlr.AgentFramework.Generators;

/// <summary>
/// Discovered [ProgressSinks] attribute on an agent class.
/// </summary>
internal readonly struct ProgressSinksEntry
{
    public ProgressSinksEntry(string agentTypeName, string agentClassName, ImmutableArray<string> sinkTypeFQNs)
    {
        AgentTypeName = agentTypeName;
        AgentClassName = agentClassName;
        SinkTypeFQNs = sinkTypeFQNs;
    }

    /// <summary>Fully-qualified agent type name.</summary>
    public string AgentTypeName { get; }

    /// <summary>Short class name.</summary>
    public string AgentClassName { get; }

    /// <summary>Fully-qualified sink type names.</summary>
    public ImmutableArray<string> SinkTypeFQNs { get; }
}
