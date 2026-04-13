// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct AgentFunctionParameterInfo
{
    public AgentFunctionParameterInfo(
        string name, string typeFullName,
        string jsonSchemaType, string? itemJsonSchemaType,
        string? itemObjectSchemaJson,
        IReadOnlyList<ObjectPropertyInfo>? itemObjectProperties,
        bool isCancellationToken, bool isNullable, bool hasDefault, string? description)
    {
        Name = name; TypeFullName = typeFullName;
        JsonSchemaType = jsonSchemaType; ItemJsonSchemaType = itemJsonSchemaType;
        ItemObjectSchemaJson = itemObjectSchemaJson;
        ItemObjectProperties = itemObjectProperties;
        IsCancellationToken = isCancellationToken; IsNullable = isNullable;
        HasDefault = hasDefault; Description = description;
    }

    public string Name { get; }
    public string TypeFullName { get; }
    public string JsonSchemaType { get; }
    /// <summary>JSON schema type for array items (e.g., "string", "integer", "object").</summary>
    public string? ItemJsonSchemaType { get; }
    /// <summary>Pre-built JSON schema for complex object array items (properties, required fields).</summary>
    public string? ItemObjectSchemaJson { get; }
    /// <summary>Property extraction info for AOT-safe manual deserialization of complex array items.</summary>
    public IReadOnlyList<ObjectPropertyInfo>? ItemObjectProperties { get; }
    public bool IsCancellationToken { get; }
    public bool IsNullable { get; }
    public bool HasDefault { get; }
    public string? Description { get; }
    public bool IsRequired => !IsCancellationToken && !IsNullable && !HasDefault;
}
