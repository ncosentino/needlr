using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Roslyn.Shared;

/// <summary>
/// Shared helper utilities for discovering injectable types from Roslyn symbols.
/// Used by both Generators and Analyzers to ensure consistent type discovery logic.
/// </summary>
public static class TypeDiscoveryHelper
{
    private const string DoNotAutoRegisterAttributeName = "DoNotAutoRegisterAttribute";
    private const string DoNotAutoRegisterAttributeFullName = "NexusLabs.Needlr.DoNotAutoRegisterAttribute";
    private const string DoNotInjectAttributeName = "DoNotInjectAttribute";
    private const string DoNotInjectAttributeFullName = "NexusLabs.Needlr.DoNotInjectAttribute";

    /// <summary>
    /// Determines whether a type symbol represents a concrete injectable type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <param name="isCurrentAssembly">True if the type is from the current compilation's assembly (allows internal types).</param>
    /// <returns>True if the type is a valid injectable type; otherwise, false.</returns>
    public static bool IsInjectableType(INamedTypeSymbol typeSymbol, bool isCurrentAssembly = false)
    {
        // Must be a class (not interface, struct, enum, delegate)
        if (typeSymbol.TypeKind != TypeKind.Class)
            return false;

        // Must be accessible from generated code
        // - Current assembly: internal and public types are accessible
        // - Referenced assemblies: only public types are accessible
        if (!IsAccessibleFromGeneratedCode(typeSymbol, isCurrentAssembly))
            return false;

        if (typeSymbol.IsAbstract)
            return false;

        if (typeSymbol.IsStatic)
            return false;

        if (typeSymbol.IsUnboundGenericType)
            return false;

        // Exclude open generic types (type definitions with type parameters like MyClass<T>)
        // These cannot be instantiated directly and would produce invalid typeof() expressions
        if (typeSymbol.TypeParameters.Length > 0)
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

        // Exclude types with required members that can't be set via constructor
        // These would cause compilation errors: "Required member 'X' must be set"
        if (HasUnsatisfiedRequiredMembers(typeSymbol))
            return false;

        return true;
    }

    /// <summary>
    /// Checks if a type has required members that aren't satisfied by any constructor
    /// with [SetsRequiredMembers] attribute.
    /// </summary>
    public static bool HasUnsatisfiedRequiredMembers(INamedTypeSymbol typeSymbol)
    {
        // Check if any constructor has [SetsRequiredMembers] attribute
        foreach (var ctor in typeSymbol.InstanceConstructors)
        {
            if (ctor.IsStatic)
                continue;

            // If a constructor has [SetsRequiredMembers], it handles all required members
            foreach (var attr in ctor.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "SetsRequiredMembersAttribute" ||
                    attr.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute")
                {
                    return false; // This constructor handles required members
                }
            }
        }

        // Check for required properties (including inherited)
        var currentType = typeSymbol;
        while (currentType != null)
        {
            foreach (var member in currentType.GetMembers())
            {
                if (member is IPropertySymbol property && property.IsRequired)
                    return true;
                if (member is IFieldSymbol field && field.IsRequired)
                    return true;
            }
            currentType = currentType.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Checks if a type has the [DoNotAutoRegister] attribute (directly or on interfaces).
    /// </summary>
    public static bool HasDoNotAutoRegisterAttribute(INamedTypeSymbol typeSymbol)
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

    /// <summary>
    /// Checks if a type has the [DoNotAutoRegister] or [DoNotInject] attribute directly applied.
    /// </summary>
    public static bool HasDoNotAutoRegisterAttributeDirect(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            var name = attributeClass.Name;
            if (name == DoNotAutoRegisterAttributeName || name == DoNotInjectAttributeName)
                return true;

            var fullName = attributeClass.ToDisplayString();
            if (fullName == DoNotAutoRegisterAttributeFullName || fullName == DoNotInjectAttributeFullName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a type is compiler-generated.
    /// </summary>
    public static bool IsCompilerGenerated(INamedTypeSymbol typeSymbol)
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

    /// <summary>
    /// Checks if a type inherits from a base type by name.
    /// </summary>
    public static bool InheritsFrom(INamedTypeSymbol typeSymbol, string baseTypeName)
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

    /// <summary>
    /// Checks if a type is accessible from generated code.
    /// For types in the current assembly, internal and public types are accessible.
    /// For types in referenced assemblies, only public types are accessible.
    /// </summary>
    public static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol typeSymbol, bool isCurrentAssembly)
    {
        // Check the type itself
        var accessibility = typeSymbol.DeclaredAccessibility;

        if (isCurrentAssembly)
        {
            // For current assembly, allow public or internal
            if (accessibility != Accessibility.Public && accessibility != Accessibility.Internal)
                return false;
        }
        else
        {
            // For referenced assemblies, only public is accessible
            if (accessibility != Accessibility.Public)
                return false;
        }

        // Check all containing types (for nested types)
        var containingType = typeSymbol.ContainingType;
        while (containingType != null)
        {
            var containingAccessibility = containingType.DeclaredAccessibility;
            if (isCurrentAssembly)
            {
                if (containingAccessibility != Accessibility.Public && containingAccessibility != Accessibility.Internal)
                    return false;
            }
            else
            {
                if (containingAccessibility != Accessibility.Public)
                    return false;
            }
            containingType = containingType.ContainingType;
        }

        return true;
    }

    /// <summary>
    /// Checks if a type is from the System namespace or system assemblies.
    /// </summary>
    public static bool IsSystemType(INamedTypeSymbol typeSymbol)
    {
        var ns = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        // Skip types from system assemblies
        if (ns.StartsWith("System", StringComparison.Ordinal))
            return true;

        // Check if from mscorlib or similar
        var assembly = typeSymbol.ContainingAssembly;
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
