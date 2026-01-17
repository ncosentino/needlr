using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Helper utilities for discovering injectable types from Roslyn symbols.
/// </summary>
internal static class TypeDiscoveryHelper
{
    private const string DoNotAutoRegisterAttributeName = "DoNotAutoRegisterAttribute";
    private const string DoNotAutoRegisterAttributeFullName = "NexusLabs.Needlr.DoNotAutoRegisterAttribute";

    /// <summary>
    /// Determines whether a type symbol represents a concrete injectable type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type is a valid injectable type; otherwise, false.</returns>
    public static bool IsInjectableType(INamedTypeSymbol typeSymbol)
    {
        // Must be a class (not interface, struct, enum, delegate)
        if (typeSymbol.TypeKind != TypeKind.Class)
            return false;

        if (typeSymbol.IsAbstract)
            return false;

        if (typeSymbol.IsStatic)
            return false;

        if (typeSymbol.IsUnboundGenericType)
            return false;

        if (typeSymbol.ContainingType != null)
            return false;

        if (IsCompilerGenerated(typeSymbol))
            return false;

        if (InheritsFrom(typeSymbol, "System.Exception"))
            return false;

        if (InheritsFrom(typeSymbol, "System.Attribute"))
            return false;

        if (typeSymbol.IsRecord)
            return false;

        if (HasDoNotAutoRegisterAttribute(typeSymbol))
            return false;

        return true;
    }

    /// <summary>
    /// Gets the interfaces that should be registered for a type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to get interfaces for.</param>
    /// <returns>A list of interface symbols suitable for registration.</returns>
    public static IReadOnlyList<INamedTypeSymbol> GetRegisterableInterfaces(INamedTypeSymbol typeSymbol)
    {
        var result = new List<INamedTypeSymbol>();

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.IsUnboundGenericType)
                continue;

            if (IsSystemInterface(iface))
                continue;

            if (HasDoNotAutoRegisterAttributeDirect(iface))
                continue;

            result.Add(iface);
        }

        return result;
    }

    /// <summary>
    /// Gets the fully qualified name for a type symbol suitable for code generation.
    /// </summary>
    /// <param name="typeSymbol">The type symbol.</param>
    /// <returns>The fully qualified type name with global:: prefix.</returns>
    public static string GetFullyQualifiedName(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// Checks if a type matches any of the given namespace prefixes.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <param name="namespacePrefixes">The namespace prefixes to match.</param>
    /// <returns>True if the type's namespace starts with any of the prefixes.</returns>
    public static bool MatchesNamespacePrefix(INamedTypeSymbol typeSymbol, IReadOnlyList<string>? namespacePrefixes)
    {
        if (namespacePrefixes == null || namespacePrefixes.Count == 0)
            return true;

        var typeNamespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        foreach (var prefix in namespacePrefixes)
        {
            if (typeNamespace.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Recursively iterates all named type symbols in a namespace.
    /// </summary>
    /// <param name="namespaceSymbol">The namespace to iterate.</param>
    /// <returns>All named type symbols in the namespace and nested namespaces.</returns>
    public static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var member in namespaceSymbol.GetMembers())
        {
            if (member is INamedTypeSymbol typeSymbol)
            {
                yield return typeSymbol;
            }
            else if (member is INamespaceSymbol nestedNamespace)
            {
                foreach (var nestedType in GetAllTypes(nestedNamespace))
                {
                    yield return nestedType;
                }
            }
        }
    }

    private static bool HasDoNotAutoRegisterAttribute(INamedTypeSymbol typeSymbol)
    {
        // Check the type itself
        if (HasDoNotAutoRegisterAttributeDirect(typeSymbol))
            return true;

        // Check all implemented interfaces
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (HasDoNotAutoRegisterAttributeDirect(iface))
                return true;
        }

        return false;
    }

    private static bool HasDoNotAutoRegisterAttributeDirect(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            var name = attributeClass.Name;
            if (name == DoNotAutoRegisterAttributeName)
                return true;

            var fullName = attributeClass.ToDisplayString();
            if (fullName == DoNotAutoRegisterAttributeFullName)
                return true;
        }

        return false;
    }

    private static bool IsCompilerGenerated(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            if (name == "System.Runtime.CompilerServices.CompilerGeneratedAttribute")
                return true;
        }

        // Also check if the name indicates compiler generation
        var typeName = typeSymbol.Name;
        if (typeName.StartsWith("<", StringComparison.Ordinal) ||
            typeName.Contains("__") ||
            typeName.Contains("<>"))
        {
            return true;
        }

        return false;
    }

    private static bool InheritsFrom(INamedTypeSymbol typeSymbol, string baseTypeName)
    {
        var currentType = typeSymbol.BaseType;
        while (currentType != null)
        {
            if (currentType.ToDisplayString() == baseTypeName)
                return true;
            currentType = currentType.BaseType;
        }
        return false;
    }

    private static bool IsSystemInterface(INamedTypeSymbol interfaceSymbol)
    {
        var ns = interfaceSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        // Skip interfaces from system assemblies
        if (ns.StartsWith("System", StringComparison.Ordinal))
            return true;

        // Check if from mscorlib or similar
        var assembly = interfaceSymbol.ContainingAssembly;
        if (assembly != null)
        {
            var assemblyName = assembly.Name;
            if (assemblyName == "mscorlib" ||
                assemblyName == "System.Runtime" ||
                assemblyName == "System.Private.CoreLib" ||
                assemblyName == "netstandard")
            {
                return true;
            }
        }

        return false;
    }
}
