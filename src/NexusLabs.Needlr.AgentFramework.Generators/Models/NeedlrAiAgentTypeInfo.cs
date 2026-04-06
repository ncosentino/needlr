// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct NeedlrAiAgentTypeInfo
{
    public NeedlrAiAgentTypeInfo(
        string typeName,
        string className,
        string? namespaceName,
        bool isPartial,
        ImmutableArray<string> functionGroupNames,
        ImmutableArray<string> explicitFunctionTypeFQNs,
        bool hasExplicitFunctionTypes)
    {
        TypeName = typeName;
        ClassName = className;
        NamespaceName = namespaceName;
        IsPartial = isPartial;
        FunctionGroupNames = functionGroupNames;
        ExplicitFunctionTypeFQNs = explicitFunctionTypeFQNs;
        HasExplicitFunctionTypes = hasExplicitFunctionTypes;
    }

    public string TypeName { get; }
    public string ClassName { get; }
    public string? NamespaceName { get; }
    public bool IsPartial { get; }
    public ImmutableArray<string> FunctionGroupNames { get; }
    public ImmutableArray<string> ExplicitFunctionTypeFQNs { get; }
    public bool HasExplicitFunctionTypes { get; }
}
