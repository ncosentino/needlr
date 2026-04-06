using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Helper for discovering and analyzing options types, including positional
/// records, bindable properties, data annotations, and nested options filtering.
/// </summary>
internal static class OptionsDiscoveryHelper
{
    /// <summary>
    /// Detects whether a type is a positional record that needs a generated parameterless constructor.
    /// Returns null if not a positional record, or PositionalRecordInfo if it is.
    /// </summary>
    internal static PositionalRecordInfo? DetectPositionalRecord(INamedTypeSymbol typeSymbol)
    {
        // Must be a record
        if (!typeSymbol.IsRecord)
            return null;

        // Check for primary constructor with parameters
        // Records with positional parameters have a primary constructor generated from the record declaration
        var primaryCtor = typeSymbol.InstanceConstructors
            .FirstOrDefault(c => c.Parameters.Length > 0 && IsPrimaryConstructor(c, typeSymbol));

        if (primaryCtor == null)
            return null;

        // Check if the record has a parameterless constructor already
        // (user-defined or from record with init-only properties)
        var hasParameterlessCtor = typeSymbol.InstanceConstructors
            .Any(c => c.Parameters.Length == 0 && !c.IsImplicitlyDeclared);

        if (hasParameterlessCtor)
            return null; // Doesn't need generated constructor

        // Check if partial
        var isPartial = typeSymbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(s => s.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

        // Extract constructor parameters
        var parameters = primaryCtor.Parameters
            .Select(p => new PositionalRecordParameter(p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .ToList();

        // Get namespace
        var containingNamespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? ""
            : typeSymbol.ContainingNamespace.ToDisplayString();

        return new PositionalRecordInfo(
            typeSymbol.Name,
            containingNamespace,
            isPartial,
            parameters);
    }

    /// <summary>
    /// Extracts bindable properties from an options type for AOT code generation.
    /// </summary>
    internal static IReadOnlyList<OptionsPropertyInfo> ExtractBindableProperties(INamedTypeSymbol typeSymbol, HashSet<string>? visitedTypes = null)
    {
        var properties = new List<OptionsPropertyInfo>();
        visitedTypes ??= new HashSet<string>();

        // Prevent infinite recursion for circular references
        var typeFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!visitedTypes.Add(typeFullName))
        {
            return properties; // Already visited - circular reference
        }

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol property)
                continue;

            // Skip static, indexers, readonly properties without init
            if (property.IsStatic || property.IsIndexer)
                continue;

            // Must have a setter (set or init)
            if (property.SetMethod == null)
                continue;

            // Check if it's init-only
            var isInitOnly = property.SetMethod.IsInitOnly;

            // Get nullability info
            var isNullable = property.NullableAnnotation == NullableAnnotation.Annotated ||
                             (property.Type is INamedTypeSymbol namedType &&
                              namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

            var typeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Check if it's an enum type
            var isEnum = false;
            string? enumTypeName = null;
            var actualType = property.Type;

            // For nullable types, get the underlying type
            if (actualType is INamedTypeSymbol nullableType &&
                nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                nullableType.TypeArguments.Length == 1)
            {
                actualType = nullableType.TypeArguments[0];
            }

            if (actualType.TypeKind == TypeKind.Enum)
            {
                isEnum = true;
                enumTypeName = actualType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            // Detect complex types
            var (complexKind, elementTypeName, nestedProps) = AnalyzeComplexType(property.Type, visitedTypes);

            // Extract DataAnnotation attributes
            var dataAnnotations = ExtractDataAnnotations(property);

            properties.Add(new OptionsPropertyInfo(
                property.Name,
                typeName,
                isNullable,
                isInitOnly,
                isEnum,
                enumTypeName,
                complexKind,
                elementTypeName,
                nestedProps,
                dataAnnotations));
        }

        return properties;
    }

    /// <summary>
    /// Extracts DataAnnotation validation attributes from a property symbol.
    /// </summary>
    internal static IReadOnlyList<DataAnnotationInfo> ExtractDataAnnotations(IPropertySymbol property)
    {
        var annotations = new List<DataAnnotationInfo>();

        foreach (var attr in property.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass == null) continue;

            // Get the attribute type name - use ContainingNamespace + Name for reliable matching
            var attrNamespace = attrClass.ContainingNamespace?.ToDisplayString() ?? "";
            var attrTypeName = attrClass.Name;

            // Only process System.ComponentModel.DataAnnotations attributes
            if (attrNamespace != "System.ComponentModel.DataAnnotations")
                continue;

            // Extract error message if present
            string? errorMessage = null;
            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "ErrorMessage" && namedArg.Value.Value is string msg)
                {
                    errorMessage = msg;
                    break;
                }
            }

            // Check for known DataAnnotation attributes
            if (attrTypeName == "RequiredAttribute")
            {
                annotations.Add(new DataAnnotationInfo(DataAnnotationKind.Required, errorMessage));
            }
            else if (attrTypeName == "RangeAttribute")
            {
                object? min = null, max = null;
                if (attr.ConstructorArguments.Length >= 2)
                {
                    min = attr.ConstructorArguments[0].Value;
                    max = attr.ConstructorArguments[1].Value;
                }
                annotations.Add(new DataAnnotationInfo(DataAnnotationKind.Range, errorMessage, min, max));
            }
            else if (attrTypeName == "StringLengthAttribute")
            {
                object? maxLen = null;
                int? minLen = null;
                if (attr.ConstructorArguments.Length >= 1)
                {
                    maxLen = attr.ConstructorArguments[0].Value;
                }
                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "MinimumLength" && namedArg.Value.Value is int ml)
                    {
                        minLen = ml;
                    }
                }
                annotations.Add(new DataAnnotationInfo(DataAnnotationKind.StringLength, errorMessage, null, maxLen, null, minLen));
            }
            else if (attrTypeName == "MinLengthAttribute")
            {
                int? minLen = null;
                if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Value is int ml)
                {
                    minLen = ml;
                }
                annotations.Add(new DataAnnotationInfo(DataAnnotationKind.MinLength, errorMessage, null, null, null, minLen));
            }
            else if (attrTypeName == "MaxLengthAttribute")
            {
                object? maxLen = null;
                if (attr.ConstructorArguments.Length >= 1)
                {
                    maxLen = attr.ConstructorArguments[0].Value;
                }
                annotations.Add(new DataAnnotationInfo(DataAnnotationKind.MaxLength, errorMessage, null, maxLen));
            }
            else if (attrTypeName == "RegularExpressionAttribute")
            {
                string? pattern = null;
                if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Value is string p)
                {
                    pattern = p;
                }
                annotations.Add(new DataAnnotationInfo(DataAnnotationKind.RegularExpression, errorMessage, null, null, pattern));
            }
            else if (attrTypeName == "EmailAddressAttribute")
            {
                annotations.Add(new DataAnnotationInfo(DataAnnotationKind.EmailAddress, errorMessage));
            }
            else if (attrTypeName == "PhoneAttribute")
            {
                annotations.Add(new DataAnnotationInfo(DataAnnotationKind.Phone, errorMessage));
            }
            else if (attrTypeName == "UrlAttribute")
            {
                annotations.Add(new DataAnnotationInfo(DataAnnotationKind.Url, errorMessage));
            }
            else if (IsValidationAttribute(attrClass))
            {
                // Unsupported validation attribute
                annotations.Add(new DataAnnotationInfo(DataAnnotationKind.Unsupported, errorMessage));
            }
        }

        return annotations;
    }

    /// <summary>
    /// Attempts to extract nested properties from a type if it is a bindable class.
    /// </summary>
    internal static IReadOnlyList<OptionsPropertyInfo>? TryGetNestedProperties(ITypeSymbol elementType, HashSet<string> visitedTypes)
    {
        if (elementType is INamedTypeSymbol namedElement && IsBindableClass(namedElement))
        {
            var props = ExtractBindableProperties(namedElement, visitedTypes);
            return props.Count > 0 ? props : null;
        }
        return null;
    }

    /// <summary>
    /// Filters out nested options types that are used as properties in other options types.
    /// These should not be registered separately - they are bound as part of their parent.
    /// </summary>
    internal static List<DiscoveredOptions> FilterNestedOptions(List<DiscoveredOptions> options, Compilation compilation)
    {
        // Build a set of all options type names
        var optionsTypeNames = new HashSet<string>(options.Select(o => o.TypeName));

        // Find all options types that are used as properties in other options types
        var nestedTypeNames = new HashSet<string>();

        foreach (var opt in options)
        {
            // Find the type symbol for this options type
            var typeSymbol = FindTypeSymbol(compilation, opt.TypeName);
            if (typeSymbol == null)
                continue;

            // Check all properties of this type
            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is not IPropertySymbol property)
                    continue;

                // Skip non-class property types (primitives, structs, etc.)
                if (property.Type is not INamedTypeSymbol propertyType)
                    continue;

                if (propertyType.TypeKind != TypeKind.Class)
                    continue;

                // Get the fully qualified name of the property type
                var propertyTypeName = TypeDiscoveryHelper.GetFullyQualifiedName(propertyType);

                // If this property type is also an [Options] type, mark it as nested
                if (optionsTypeNames.Contains(propertyTypeName))
                {
                    nestedTypeNames.Add(propertyTypeName);
                }
            }
        }

        // Return only root options (those not used as properties in other options)
        return options.Where(o => !nestedTypeNames.Contains(o.TypeName)).ToList();
    }

    private static bool IsPrimaryConstructor(IMethodSymbol ctor, INamedTypeSymbol recordType)
    {
        // For positional records, the primary constructor parameters correspond to auto-properties
        // Check if each parameter has a matching property
        foreach (var param in ctor.Parameters)
        {
            var hasMatchingProperty = recordType.GetMembers()
                .OfType<IPropertySymbol>()
                .Any(p => p.Name.Equals(param.Name, StringComparison.Ordinal) &&
                         SymbolEqualityComparer.Default.Equals(p.Type, param.Type));

            if (!hasMatchingProperty)
                return false;
        }

        return true;
    }

    private static (ComplexTypeKind Kind, string? ElementTypeName, IReadOnlyList<OptionsPropertyInfo>? NestedProperties) AnalyzeComplexType(
        ITypeSymbol typeSymbol,
        HashSet<string> visitedTypes)
    {
        // Check for array
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            var elementType = arrayType.ElementType;
            var elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var nestedProps = TryGetNestedProperties(elementType, visitedTypes);
            return (ComplexTypeKind.Array, elementTypeName, nestedProps);
        }

        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return (ComplexTypeKind.None, null, null);
        }

        // Check for Dictionary<string, T>
        if (IsDictionaryType(namedType))
        {
            var valueType = namedType.TypeArguments[1];
            var valueTypeName = valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var nestedProps = TryGetNestedProperties(valueType, visitedTypes);
            return (ComplexTypeKind.Dictionary, valueTypeName, nestedProps);
        }

        // Check for List<T>, IList<T>, ICollection<T>, IEnumerable<T>
        if (IsListType(namedType))
        {
            var elementType = namedType.TypeArguments[0];
            var elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var nestedProps = TryGetNestedProperties(elementType, visitedTypes);
            return (ComplexTypeKind.List, elementTypeName, nestedProps);
        }

        // Check for nested object (class with bindable properties)
        if (IsBindableClass(namedType))
        {
            var nestedProps = ExtractBindableProperties(namedType, visitedTypes);
            if (nestedProps.Count > 0)
            {
                return (ComplexTypeKind.NestedObject, null, nestedProps);
            }
        }

        return (ComplexTypeKind.None, null, null);
    }

    private static bool IsValidationAttribute(INamedTypeSymbol attrClass)
    {
        // Check if this inherits from ValidationAttribute
        var current = attrClass.BaseType;
        while (current != null)
        {
            if (current.ToDisplayString() == "System.ComponentModel.DataAnnotations.ValidationAttribute")
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static bool IsDictionaryType(INamedTypeSymbol type)
    {
        // Check for Dictionary<TKey, TValue> or IDictionary<TKey, TValue>
        if (type.TypeArguments.Length != 2)
            return false;

        var typeName = type.OriginalDefinition.ToDisplayString();
        return typeName == "System.Collections.Generic.Dictionary<TKey, TValue>" ||
               typeName == "System.Collections.Generic.IDictionary<TKey, TValue>";
    }

    private static bool IsListType(INamedTypeSymbol type)
    {
        if (type.TypeArguments.Length != 1)
            return false;

        var typeName = type.OriginalDefinition.ToDisplayString();
        return typeName == "System.Collections.Generic.List<T>" ||
               typeName == "System.Collections.Generic.IList<T>" ||
               typeName == "System.Collections.Generic.ICollection<T>" ||
               typeName == "System.Collections.Generic.IEnumerable<T>";
    }

    private static bool IsBindableClass(INamedTypeSymbol type)
    {
        // Must be a class or struct, not abstract, not a system type
        if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
            return false;

        if (type.IsAbstract)
            return false;

        // Skip system types and primitives
        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns.StartsWith("System"))
        {
            // Skip known non-bindable System namespaces
            if (ns == "System" || ns.StartsWith("System.Collections") || ns.StartsWith("System.Threading"))
                return false;
        }

        // Must have a parameterless constructor (explicit or implicit)
        // Note: Classes without any explicit constructors have an implicit parameterless constructor
        var hasExplicitConstructors = type.InstanceConstructors.Any(c => !c.IsImplicitlyDeclared);
        if (hasExplicitConstructors)
        {
            var hasParameterlessCtor = type.InstanceConstructors
                .Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);
            return hasParameterlessCtor;
        }

        // No explicit constructors means implicit parameterless constructor exists
        return true;
    }

    private static INamedTypeSymbol? FindTypeSymbol(Compilation compilation, string fullyQualifiedName)
    {
        // Strip global:: prefix if present
        var typeName = fullyQualifiedName.StartsWith("global::")
            ? fullyQualifiedName.Substring(8)
            : fullyQualifiedName;

        return compilation.GetTypeByMetadataName(typeName);
    }
}
