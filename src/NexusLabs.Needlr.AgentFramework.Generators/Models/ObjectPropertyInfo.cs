namespace NexusLabs.Needlr.AgentFramework.Generators;

/// <summary>
/// Describes a single property of a complex object type used as an array element
/// in an agent function parameter. Used for AOT-safe manual deserialization from
/// JsonElement.
/// </summary>
internal readonly struct ObjectPropertyInfo
{
    public ObjectPropertyInfo(string csharpName, string jsonName, string schemaType, bool isNullable)
    {
        CSharpName = csharpName;
        JsonName = jsonName;
        SchemaType = schemaType;
        IsNullable = isNullable;
    }

    /// <summary>The C# property name (PascalCase, e.g., "Topic").</summary>
    public string CSharpName { get; }

    /// <summary>The JSON property name (camelCase, e.g., "topic").</summary>
    public string JsonName { get; }

    /// <summary>The JSON schema type (e.g., "string", "integer", "boolean").</summary>
    public string SchemaType { get; }

    /// <summary>Whether the property is nullable.</summary>
    public bool IsNullable { get; }
}
