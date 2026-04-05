using System;
using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a bindable property on an options class (for AOT code generation).
/// </summary>
internal readonly struct OptionsPropertyInfo
{
    public OptionsPropertyInfo(
        string name,
        string typeName,
        bool isNullable,
        bool hasInitOnlySetter,
        bool isEnum = false,
        string? enumTypeName = null,
        ComplexTypeKind complexTypeKind = ComplexTypeKind.None,
        string? elementTypeName = null,
        IReadOnlyList<OptionsPropertyInfo>? nestedProperties = null,
        IReadOnlyList<DataAnnotationInfo>? dataAnnotations = null)
    {
        Name = name;
        TypeName = typeName;
        IsNullable = isNullable;
        HasInitOnlySetter = hasInitOnlySetter;
        IsEnum = isEnum;
        EnumTypeName = enumTypeName;
        ComplexTypeKind = complexTypeKind;
        ElementTypeName = elementTypeName;
        NestedProperties = nestedProperties;
        DataAnnotations = dataAnnotations ?? Array.Empty<DataAnnotationInfo>();
    }

    /// <summary>Property name.</summary>
    public string Name { get; }

    /// <summary>Fully qualified type name.</summary>
    public string TypeName { get; }

    /// <summary>True if the property type is nullable.</summary>
    public bool IsNullable { get; }

    /// <summary>True if the property has an init-only setter.</summary>
    public bool HasInitOnlySetter { get; }

    /// <summary>True if the property type is an enum.</summary>
    public bool IsEnum { get; }

    /// <summary>The underlying enum type name (for nullable enums, this is the non-nullable type).</summary>
    public string? EnumTypeName { get; }

    /// <summary>The kind of complex type (nested object, array, list, dictionary).</summary>
    public ComplexTypeKind ComplexTypeKind { get; }

    /// <summary>For collections, the element type. For dictionaries, the value type.</summary>
    public string? ElementTypeName { get; }

    /// <summary>For nested objects and collection element types, the bindable properties.</summary>
    public IReadOnlyList<OptionsPropertyInfo>? NestedProperties { get; }

    /// <summary>DataAnnotation validation attributes on this property.</summary>
    public IReadOnlyList<DataAnnotationInfo> DataAnnotations { get; }

    /// <summary>True if this property has any DataAnnotation validation attributes.</summary>
    public bool HasDataAnnotations => DataAnnotations.Count > 0;
}
