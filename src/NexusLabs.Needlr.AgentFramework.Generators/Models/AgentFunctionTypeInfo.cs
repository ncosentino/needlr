// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct AgentFunctionTypeInfo
{
    public AgentFunctionTypeInfo(string typeName, string assemblyName, bool isStatic, ImmutableArray<AgentFunctionMethodInfo> methods)
    {
        TypeName = typeName; AssemblyName = assemblyName; IsStatic = isStatic; Methods = methods;
    }

    public string TypeName { get; }
    public string AssemblyName { get; }
    public bool IsStatic { get; }
    public ImmutableArray<AgentFunctionMethodInfo> Methods { get; }
}
