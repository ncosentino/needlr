// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct AgentFunctionMethodInfo
{
    public AgentFunctionMethodInfo(
        string methodName, bool isAsync, bool isVoidLike,
        string? returnValueTypeFQN, ImmutableArray<AgentFunctionParameterInfo> parameters,
        string description)
    {
        MethodName = methodName; IsAsync = isAsync; IsVoidLike = isVoidLike;
        ReturnValueTypeFQN = returnValueTypeFQN; Parameters = parameters; Description = description;
    }

    public string MethodName { get; }
    public bool IsAsync { get; }
    public bool IsVoidLike { get; }
    public string? ReturnValueTypeFQN { get; }
    public ImmutableArray<AgentFunctionParameterInfo> Parameters { get; }
    public string Description { get; }
}
