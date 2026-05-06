namespace NexusLabs.Needlr.AgentFramework.Generators;

/// <summary>
/// Describes a single property of a complex object type used as an array element
/// in an agent function parameter. Used for AOT-safe manual deserialization from
/// JsonElement.
/// </summary>
internal readonly struct ObjectPropertyInfo
{
    public ObjectPropertyInfo(
        string csharpName,
        string jsonName,
        string csharpTypeFullName,
        string schemaType,
        string? schemaFormat,
        bool isNullable,
        string? initDefaultLiteral)
    {
        CSharpName = csharpName;
        JsonName = jsonName;
        CSharpTypeFullName = csharpTypeFullName;
        SchemaType = schemaType;
        SchemaFormat = schemaFormat;
        IsNullable = isNullable;
        InitDefaultLiteral = initDefaultLiteral;
    }

    /// <summary>The C# property name (PascalCase, e.g., "Topic").</summary>
    public string CSharpName { get; }

    /// <summary>The JSON property name (camelCase, e.g., "topic").</summary>
    public string JsonName { get; }

    /// <summary>The fully-qualified C# type name (e.g., <c>"global::System.DateTimeOffset"</c>).</summary>
    public string CSharpTypeFullName { get; }

    /// <summary>The JSON schema type (e.g., "string", "integer", "boolean").</summary>
    public string SchemaType { get; }

    /// <summary>
    /// JSON Schema <c>format</c> hint for stringified value types (e.g., <c>"uuid"</c>,
    /// <c>"date-time"</c>, <c>"duration"</c>). <see langword="null"/> when no format applies.
    /// </summary>
    public string? SchemaFormat { get; }

    /// <summary>Whether the property is nullable.</summary>
    public bool IsNullable { get; }

    /// <summary>
    /// The C# literal expression for the property's initializer default, when present and
    /// expressible as a simple literal. For example, <c>"\"default\""</c> for
    /// <c>public string Foo { get; init; } = "default";</c> or <c>"5"</c> for
    /// <c>public int Count { get; init; } = 5;</c>. <see langword="null"/> when the
    /// property has no initializer (or the initializer is not a simple literal expression).
    /// Emitted as the fallback when a DTO payload supplies the property as
    /// <c>JsonValueKind.Null</c> / <c>JsonValueKind.Undefined</c>.
    /// </summary>
    public string? InitDefaultLiteral { get; }
}
