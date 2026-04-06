// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct AgentFunctionParameterInfo
{
    public AgentFunctionParameterInfo(
        string name, string typeFullName,
        string jsonSchemaType, string? itemJsonSchemaType,
        bool isCancellationToken, bool isNullable, bool hasDefault, string? description)
    {
        Name = name; TypeFullName = typeFullName;
        JsonSchemaType = jsonSchemaType; ItemJsonSchemaType = itemJsonSchemaType;
        IsCancellationToken = isCancellationToken; IsNullable = isNullable;
        HasDefault = hasDefault; Description = description;
    }

    public string Name { get; }
    public string TypeFullName { get; }
    public string JsonSchemaType { get; }
    public string? ItemJsonSchemaType { get; }
    public bool IsCancellationToken { get; }
    public bool IsNullable { get; }
    public bool HasDefault { get; }
    public string? Description { get; }
    public bool IsRequired => !IsCancellationToken && !IsNullable && !HasDefault;
}
