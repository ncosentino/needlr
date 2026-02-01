using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Helper for discovering [Provider] attributes from Roslyn symbols.
/// </summary>
internal static class ProviderDiscoveryHelper
{
    private const string ProviderAttributeName = "ProviderAttribute";
    private const string ProviderAttributeFullName = "NexusLabs.Needlr.Generators.ProviderAttribute";

    /// <summary>
    /// Checks if a type has the [Provider] attribute.
    /// </summary>
    public static bool HasProviderAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            var name = attributeClass.Name;
            if (name == ProviderAttributeName)
                return true;

            var fullName = attributeClass.ToDisplayString();
            if (fullName == ProviderAttributeFullName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the [Provider] attribute data from a type.
    /// </summary>
    public static AttributeData? GetProviderAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            var name = attributeClass.Name;
            var fullName = attributeClass.ToDisplayString();

            if (name == ProviderAttributeName || fullName == ProviderAttributeFullName)
                return attribute;
        }

        return null;
    }

    /// <summary>
    /// Discovers a provider from a type symbol with [Provider] attribute.
    /// </summary>
    public static DiscoveredProvider? DiscoverProvider(INamedTypeSymbol typeSymbol, string assemblyName)
    {
        var providerAttr = GetProviderAttribute(typeSymbol);
        if (providerAttr == null)
            return null;

        var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
        var isInterface = typeSymbol.TypeKind == TypeKind.Interface;
        var isPartial = IsPartialType(typeSymbol);
        var sourceFilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;

        var properties = new List<ProviderPropertyInfo>();

        if (isInterface)
        {
            // Interface mode: Extract properties from interface definition
            properties.AddRange(ExtractPropertiesFromInterface(typeSymbol));
        }
        else
        {
            // Class mode: Extract from attribute constructor args and named args
            properties.AddRange(ExtractPropertiesFromAttribute(providerAttr));
        }

        return new DiscoveredProvider(
            typeName,
            assemblyName,
            isInterface,
            isPartial,
            properties,
            sourceFilePath);
    }

    /// <summary>
    /// Checks if a type is declared as partial.
    /// </summary>
    private static bool IsPartialType(INamedTypeSymbol typeSymbol)
    {
        foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax();
            if (syntax is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax typeDecl)
            {
                if (typeDecl.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Extracts provider properties from an interface's get-only properties.
    /// </summary>
    private static IEnumerable<ProviderPropertyInfo> ExtractPropertiesFromInterface(INamedTypeSymbol interfaceSymbol)
    {
        foreach (var member in interfaceSymbol.GetMembers())
        {
            if (member is IPropertySymbol property && property.GetMethod != null && property.SetMethod == null)
            {
                var propertyName = property.Name;
                if (property.Type is INamedTypeSymbol namedType)
                {
                    var serviceTypeName = TypeDiscoveryHelper.GetFullyQualifiedName(namedType);
                    var kind = DeterminePropertyKind(property.Type, property.NullableAnnotation);

                    yield return new ProviderPropertyInfo(propertyName, serviceTypeName, kind);
                }
            }
        }
    }

    /// <summary>
    /// Extracts provider properties from [Provider] attribute arguments.
    /// </summary>
    private static IEnumerable<ProviderPropertyInfo> ExtractPropertiesFromAttribute(AttributeData attribute)
    {
        // Process constructor arguments (required services)
        if (attribute.ConstructorArguments.Length > 0)
        {
            var firstArg = attribute.ConstructorArguments[0];
            if (firstArg.Kind == TypedConstantKind.Array)
            {
                foreach (var typeArg in firstArg.Values)
                {
                    if (typeArg.Value is INamedTypeSymbol typeSymbol)
                    {
                        var propertyName = DerivePropertyName(typeSymbol);
                        var serviceTypeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                        yield return new ProviderPropertyInfo(propertyName, serviceTypeName, ProviderPropertyKind.Required);
                    }
                }
            }
        }

        // Process named arguments
        foreach (var namedArg in attribute.NamedArguments)
        {
            var kind = namedArg.Key switch
            {
                "Required" => ProviderPropertyKind.Required,
                "Optional" => ProviderPropertyKind.Optional,
                "Collections" => ProviderPropertyKind.Collection,
                "Factories" => ProviderPropertyKind.Factory,
                _ => (ProviderPropertyKind?)null
            };

            if (kind.HasValue && namedArg.Value.Kind == TypedConstantKind.Array)
            {
                foreach (var typeArg in namedArg.Value.Values)
                {
                    if (typeArg.Value is INamedTypeSymbol typeSymbol)
                    {
                        var propertyName = DerivePropertyName(typeSymbol);
                        var serviceTypeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                        yield return new ProviderPropertyInfo(propertyName, serviceTypeName, kind.Value);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Derives a property name from a type (e.g., IOrderRepository → OrderRepository).
    /// </summary>
    private static string DerivePropertyName(INamedTypeSymbol typeSymbol)
    {
        var name = typeSymbol.Name;

        // Remove leading 'I' from interface names
        if (typeSymbol.TypeKind == TypeKind.Interface && name.StartsWith("I") && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name.Substring(1);
        }

        return name;
    }

    /// <summary>
    /// Determines the property kind based on the type.
    /// </summary>
    private static ProviderPropertyKind DeterminePropertyKind(ITypeSymbol type, NullableAnnotation nullableAnnotation)
    {
        // Check for IEnumerable<T>
        if (type is INamedTypeSymbol namedType)
        {
            var displayName = namedType.OriginalDefinition.ToDisplayString();
            if (displayName.StartsWith("System.Collections.Generic.IEnumerable<") ||
                displayName.StartsWith("System.Collections.Generic.IReadOnlyCollection<") ||
                displayName.StartsWith("System.Collections.Generic.IReadOnlyList<"))
            {
                return ProviderPropertyKind.Collection;
            }
        }

        // Check for nullable annotation
        if (nullableAnnotation == NullableAnnotation.Annotated)
        {
            return ProviderPropertyKind.Optional;
        }

        return ProviderPropertyKind.Required;
    }
}
