// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal readonly struct AgentFunctionParameterInfo
{
    public AgentFunctionParameterInfo(
        string name, string typeFullName,
        string jsonSchemaType, string? jsonSchemaFormat,
        string? itemJsonSchemaType,
        string? itemObjectSchemaJson,
        IReadOnlyList<ObjectPropertyInfo>? itemObjectProperties,
        string? objectSchemaJson,
        IReadOnlyList<ObjectPropertyInfo>? objectProperties,
        bool isCancellationToken, bool isNullable, bool hasDefault,
        string? defaultLiteral, bool isEnum, string? description)
    {
        Name = name; TypeFullName = typeFullName;
        JsonSchemaType = jsonSchemaType; JsonSchemaFormat = jsonSchemaFormat;
        ItemJsonSchemaType = itemJsonSchemaType;
        ItemObjectSchemaJson = itemObjectSchemaJson;
        ItemObjectProperties = itemObjectProperties;
        ObjectSchemaJson = objectSchemaJson;
        ObjectProperties = objectProperties;
        IsCancellationToken = isCancellationToken; IsNullable = isNullable;
        HasDefault = hasDefault; DefaultLiteral = defaultLiteral;
        IsEnum = isEnum; Description = description;
    }

    public string Name { get; }
    public string TypeFullName { get; }
    public string JsonSchemaType { get; }
    /// <summary>
    /// JSON Schema <c>format</c> hint for stringified value types (e.g., <c>"uuid"</c> for
    /// <see cref="System.Guid"/>, <c>"date-time"</c> for <see cref="System.DateTime"/>,
    /// <c>"duration"</c> for <see cref="System.TimeSpan"/>). <see langword="null"/> when no
    /// format applies.
    /// </summary>
    public string? JsonSchemaFormat { get; }
    /// <summary>JSON schema type for array items (e.g., "string", "integer", "object").</summary>
    public string? ItemJsonSchemaType { get; }
    /// <summary>Pre-built JSON schema for complex object array items (properties, required fields).</summary>
    public string? ItemObjectSchemaJson { get; }
    /// <summary>Property extraction info for AOT-safe manual deserialization of complex array items.</summary>
    public IReadOnlyList<ObjectPropertyInfo>? ItemObjectProperties { get; }
    /// <summary>
    /// Pre-built JSON schema for a top-level complex object parameter (properties, required
    /// fields). Set when <see cref="JsonSchemaType"/> is <c>"object"</c> AND the type has
    /// public properties suitable for property-level binding.
    /// </summary>
    public string? ObjectSchemaJson { get; }
    /// <summary>
    /// Property extraction info for AOT-safe manual deserialization of a top-level complex
    /// object parameter. Mirrors <see cref="ItemObjectProperties"/> but applies to the
    /// parameter type itself rather than an array element.
    /// </summary>
    public IReadOnlyList<ObjectPropertyInfo>? ObjectProperties { get; }
    public bool IsCancellationToken { get; }
    public bool IsNullable { get; }
    public bool HasDefault { get; }
    /// <summary>
    /// The C# literal expression for the parameter's default value, when
    /// <see cref="HasDefault"/> is <see langword="true"/>. For example, <c>"false"</c> for
    /// <c>bool flag = false</c>, <c>"5"</c> for <c>int max = 5</c>, <c>"\"x\""</c> for
    /// <c>string p = "x"</c>, or <c>"null"</c> for <c>string? p = null</c>. The literal is
    /// emitted directly into generated extraction code as the fallback when the model omits
    /// the argument or supplies <c>JsonValueKind.Null</c> / <c>JsonValueKind.Undefined</c>.
    /// </summary>
    public string? DefaultLiteral { get; }
    /// <summary>
    /// <see langword="true"/> when the parameter type is a C# <see langword="enum"/>. Drives
    /// enum-aware extraction emission: the wrapper calls
    /// <c>GetStringArgument</c> then <c>Enum.Parse&lt;T&gt;(s, ignoreCase: true)</c> rather
    /// than directly assigning a <see cref="string"/> to an enum-typed variable.
    /// </summary>
    public bool IsEnum { get; }
    public string? Description { get; }
    public bool IsRequired => !IsCancellationToken && !IsNullable && !HasDefault;
}
