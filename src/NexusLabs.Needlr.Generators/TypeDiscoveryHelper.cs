using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Internal lifetime enum used by the generator to avoid runtime dependency on Attributes assembly.
/// Maps 1:1 with InjectableLifetime in the Attributes package.
/// </summary>
internal enum GeneratorLifetime
{
    Singleton = 0,
    Scoped = 1,
    Transient = 2
}

/// <summary>
/// Helper utilities for discovering injectable types from Roslyn symbols.
/// </summary>
internal static class TypeDiscoveryHelper
{
    private const string DoNotAutoRegisterAttributeName = "DoNotAutoRegisterAttribute";
    private const string DoNotAutoRegisterAttributeFullName = "NexusLabs.Needlr.DoNotAutoRegisterAttribute";
    private const string DoNotInjectAttributeName = "DoNotInjectAttribute";
    private const string DoNotInjectAttributeFullName = "NexusLabs.Needlr.DoNotInjectAttribute";
    private const string DeferToContainerAttributeName = "DeferToContainerAttribute";
    private const string DeferToContainerAttributeFullName = "NexusLabs.Needlr.DeferToContainerAttribute";

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

        // Get all constructor parameter types to detect decorator pattern
        var constructorParamTypes = GetConstructorParameterTypes(typeSymbol);

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.IsUnboundGenericType)
                continue;

            if (IsSystemInterface(iface))
                continue;

            if (HasDoNotAutoRegisterAttributeDirect(iface))
                continue;

            // Skip interfaces that this type also takes as constructor parameters (decorator pattern)
            // A type that implements IFoo and takes IFoo in its constructor is likely a decorator
            // and should not be auto-registered as IFoo to avoid circular dependencies
            if (IsDecoratorInterface(iface, constructorParamTypes))
                continue;

            result.Add(iface);
        }

        return result;
    }

    /// <summary>
    /// Gets all parameter types from all constructors of a type.
    /// </summary>
    private static HashSet<string> GetConstructorParameterTypes(INamedTypeSymbol typeSymbol)
    {
        var paramTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var ctor in typeSymbol.InstanceConstructors)
        {
            if (ctor.IsStatic)
                continue;

            foreach (var param in ctor.Parameters)
            {
                var paramTypeName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                paramTypes.Add(paramTypeName);
            }
        }

        return paramTypes;
    }

    /// <summary>
    /// Checks if an interface is a decorator interface (also taken as a constructor parameter).
    /// </summary>
    private static bool IsDecoratorInterface(INamedTypeSymbol iface, HashSet<string> constructorParamTypes)
    {
        var ifaceName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return constructorParamTypes.Contains(ifaceName);
    }

    /// <summary>
    /// Gets the fully qualified name for a type symbol suitable for code generation.
    /// For generic type definitions (open generics), outputs open generic syntax (e.g., MyClass&lt;&gt;).
    /// For constructed generics with concrete type arguments, outputs the full type (e.g., MyClass&lt;int&gt;).
    /// </summary>
    /// <param name="typeSymbol">The type symbol.</param>
    /// <returns>The fully qualified type name with global:: prefix.</returns>
    public static string GetFullyQualifiedName(INamedTypeSymbol typeSymbol)
    {
        // Check if this is an open generic type definition (has type parameters, not type arguments)
        // e.g., JobScheduler<TJob> where TJob is a TypeParameter, not a concrete type
        // We need to convert these to open generic syntax: JobScheduler<>
        if (typeSymbol.TypeParameters.Length > 0 && !typeSymbol.IsUnboundGenericType)
        {
            // Check if type arguments are still type parameters (meaning this is a generic definition)
            // For a closed generic like ILogger<MyService>, TypeArguments contains MyService (a NamedTypeSymbol)
            // For an open generic like JobScheduler<TJob>, TypeArguments contains TJob (a TypeParameterSymbol)
            var hasUnresolvedTypeParameters = typeSymbol.TypeArguments.Any(ta => ta.TypeKind == TypeKind.TypeParameter);
            
            if (hasUnresolvedTypeParameters)
            {
                // Build the open generic name manually
                var containingNamespace = typeSymbol.ContainingNamespace?.ToDisplayString();
                var typeName = typeSymbol.Name;
                var arity = typeSymbol.TypeParameters.Length;
                
                // Create the open generic syntax: MyClass<,> for 2 type params, MyClass<> for 1
                var commas = arity > 1 ? new string(',', arity - 1) : string.Empty;
                var openGenericPart = $"<{commas}>";
                
                if (string.IsNullOrEmpty(containingNamespace) || containingNamespace == "<global namespace>")
                {
                    return $"global::{typeName}{openGenericPart}";
                }
                
                return $"global::{containingNamespace}.{typeName}{openGenericPart}";
            }
        }
        
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
        
        // Check if type is in the global namespace
        var isGlobalNamespace = typeSymbol.ContainingNamespace?.IsGlobalNamespace == true;

        foreach (var prefix in namespacePrefixes)
        {
            // Empty string prefix matches global namespace types
            if (string.IsNullOrEmpty(prefix))
            {
                if (isGlobalNamespace)
                    return true;
                continue;
            }
            
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

    /// <summary>
    /// Checks if a type is accessible from generated code.
    /// For types in the current assembly, internal and public types are accessible.
    /// For types in referenced assemblies, only public types are accessible.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <param name="isCurrentAssembly">True if the type is from the current compilation's assembly.</param>
    /// <returns>True if the type is accessible from generated code.</returns>
    private static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol typeSymbol, bool isCurrentAssembly)
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
    /// Checks if a type would be registerable as injectable, ignoring accessibility constraints.
    /// This is used to detect internal types that match namespace filters but cannot be included.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type would be injectable if it were accessible.</returns>
    public static bool WouldBeInjectableIgnoringAccessibility(INamedTypeSymbol typeSymbol)
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

        // Exclude open generic types (type definitions with type parameters like MyClass<T>)
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

        // Must have a determinable lifetime to be injectable
        var lifetime = DetermineLifetime(typeSymbol);
        if (!lifetime.HasValue)
            return false;

        return true;
    }

    /// <summary>
    /// Checks if a type would be registerable as a plugin, ignoring accessibility constraints.
    /// This is used to detect internal types that match namespace filters but cannot be included.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type would be a plugin if it were accessible.</returns>
    public static bool WouldBePluginIgnoringAccessibility(INamedTypeSymbol typeSymbol)
    {
        // Must be a concrete class
        if (typeSymbol.TypeKind != TypeKind.Class)
            return false;

        if (typeSymbol.IsAbstract)
            return false;

        if (typeSymbol.IsStatic)
            return false;

        if (typeSymbol.IsUnboundGenericType)
            return false;

        // Exclude open generic types (type definitions with type parameters like MyClass<T>)
        if (typeSymbol.TypeParameters.Length > 0)
            return false;

        // Must have a parameterless constructor
        if (!HasParameterlessConstructor(typeSymbol))
            return false;

        // Must have at least one plugin interface
        var pluginInterfaces = GetPluginInterfaces(typeSymbol);
        return pluginInterfaces.Count > 0;
    }

    /// <summary>
    /// Checks if a type is internal (not public) and would be inaccessible from generated code
    /// in a different assembly.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type is internal or less accessible.</returns>
    public static bool IsInternalOrLessAccessible(INamedTypeSymbol typeSymbol)
    {
        // Check the type itself
        if (typeSymbol.DeclaredAccessibility != Accessibility.Public)
            return true;

        // Check all containing types (for nested types)
        var containingType = typeSymbol.ContainingType;
        while (containingType != null)
        {
            if (containingType.DeclaredAccessibility != Accessibility.Public)
                return true;
            containingType = containingType.ContainingType;
        }

        return false;
    }

    /// <summary>
    /// Known Needlr plugin interface names that indicate a type is a plugin.
    /// </summary>
    private static readonly string[] NeedlrPluginInterfaceNames =
    [
        "NexusLabs.Needlr.IServiceCollectionPlugin",
        "NexusLabs.Needlr.IPostBuildServiceCollectionPlugin",
        "NexusLabs.Needlr.AspNet.IWebApplicationPlugin",
        "NexusLabs.Needlr.AspNet.IWebApplicationBuilderPlugin",
        "NexusLabs.Needlr.SignalR.IHubRegistrationPlugin",
        "NexusLabs.Needlr.SemanticKernel.IKernelBuilderPlugin",
        "NexusLabs.Needlr.Hosting.IHostApplicationBuilderPlugin",
        "NexusLabs.Needlr.Hosting.IHostPlugin"
    ];

    /// <summary>
    /// Checks if a type implements any known Needlr plugin interface.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type implements a Needlr plugin interface.</returns>
    public static bool ImplementsNeedlrPluginInterface(INamedTypeSymbol typeSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            var ifaceName = iface.ToDisplayString();
            foreach (var pluginInterface in NeedlrPluginInterfaceNames)
            {
                if (ifaceName == pluginInterface)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Gets the name of the first Needlr plugin interface implemented by the type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>The interface name, or null if none found.</returns>
    public static string? GetNeedlrPluginInterfaceName(INamedTypeSymbol typeSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            var ifaceName = iface.ToDisplayString();
            foreach (var pluginInterface in NeedlrPluginInterfaceNames)
            {
                if (ifaceName == pluginInterface)
                    return ifaceName;
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if an assembly has the [GenerateTypeRegistry] attribute.
    /// </summary>
    /// <param name="assembly">The assembly symbol to check.</param>
    /// <returns>True if the assembly has the attribute.</returns>
    public static bool HasGenerateTypeRegistryAttribute(IAssemblySymbol assembly)
    {
        const string attributeName = "NexusLabs.Needlr.Generators.GenerateTypeRegistryAttribute";
        
        foreach (var attribute in assembly.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass == null)
                continue;
                
            if (attrClass.ToDisplayString() == attributeName)
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// Checks if a type is publicly accessible (can be referenced from generated code).
    /// A type is publicly accessible if it and all its containing types are public.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type is publicly accessible.</returns>
    private static bool IsPubliclyAccessible(INamedTypeSymbol typeSymbol)
    {
        // Check the type itself
        if (typeSymbol.DeclaredAccessibility != Accessibility.Public)
            return false;

        // Check all containing types (for nested types)
        var containingType = typeSymbol.ContainingType;
        while (containingType != null)
        {
            if (containingType.DeclaredAccessibility != Accessibility.Public)
                return false;
            containingType = containingType.ContainingType;
        }

        return true;
    }

    /// <summary>
    /// Determines the injectable lifetime for a type by analyzing its constructors.
    /// This mirrors the logic in ReflectionTypeFilterer.IsInjectableSingletonType.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to analyze.</param>
    /// <returns>The determined lifetime, or null if the type is not injectable.</returns>
    public static GeneratorLifetime? DetermineLifetime(INamedTypeSymbol typeSymbol)
    {
        // Check for DoNotInjectAttribute
        if (HasDoNotInjectAttribute(typeSymbol))
            return null;

        // Exclude open generic types (type definitions with type parameters like MyClass<T>)
        if (typeSymbol.TypeParameters.Length > 0)
            return null;

        // Types with [DeferToContainer] are always injectable as Singleton
        // (the attribute declares constructor params that will be added by another generator)
        if (HasDeferToContainerAttribute(typeSymbol))
            return GeneratorLifetime.Singleton;

        // Get all instance constructors
        var constructors = typeSymbol.InstanceConstructors;

        foreach (var ctor in constructors)
        {
            // Skip static constructors
            if (ctor.IsStatic)
                continue;

            var parameters = ctor.Parameters;

            // Parameterless constructor is always valid
            if (parameters.Length == 0)
                return GeneratorLifetime.Singleton;

            // Single parameter of same type (copy constructor) - not injectable
            if (parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(parameters[0].Type, typeSymbol))
                continue;

            // Check if all parameters are injectable types
            if (AllParametersAreInjectable(parameters))
                return GeneratorLifetime.Singleton;
        }

        return null;
    }

    private static bool AllParametersAreInjectable(System.Collections.Immutable.ImmutableArray<IParameterSymbol> parameters)
    {
        foreach (var param in parameters)
        {
            if (!IsInjectableParameterType(param.Type))
                return false;
        }
        return true;
    }

    private static bool IsInjectableParameterType(ITypeSymbol typeSymbol)
    {
        // Must not be a delegate
        if (typeSymbol.TypeKind == TypeKind.Delegate)
            return false;

        // Must not be a value type
        if (typeSymbol.IsValueType)
            return false;

        // Must not be string
        if (typeSymbol.SpecialType == SpecialType.System_String)
            return false;

        // Must be a class or interface
        if (typeSymbol.TypeKind != TypeKind.Class && typeSymbol.TypeKind != TypeKind.Interface)
            return false;

        return true;
    }

    private static bool HasDoNotInjectAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            var name = attributeClass.Name;
            if (name == DoNotInjectAttributeName)
                return true;

            var fullName = attributeClass.ToDisplayString();
            if (fullName == DoNotInjectAttributeFullName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if a type is a valid plugin type (concrete class with parameterless constructor).
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <param name="isCurrentAssembly">True if the type is from the current compilation's assembly (allows internal types).</param>
    /// <returns>True if the type is a valid plugin type.</returns>
    public static bool IsPluginType(INamedTypeSymbol typeSymbol, bool isCurrentAssembly = false)
    {
        // Must be a concrete class
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

        // Must have a parameterless constructor
        if (!HasParameterlessConstructor(typeSymbol))
            return false;

        return true;
    }

    /// <summary>
    /// Gets the plugin interfaces implemented by a type.
    /// Plugin interfaces are interfaces in non-System namespaces.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>A list of plugin interface symbols.</returns>
    public static IReadOnlyList<INamedTypeSymbol> GetPluginInterfaces(INamedTypeSymbol typeSymbol)
    {
        var result = new List<INamedTypeSymbol>();

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.IsUnboundGenericType)
                continue;

            if (IsSystemInterface(iface))
                continue;

            result.Add(iface);
        }

        return result;
    }

    /// <summary>
    /// Checks if a type implements a specific interface by name.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <param name="interfaceFullName">The full name of the interface.</param>
    /// <returns>True if the type implements the interface.</returns>
    public static bool ImplementsInterface(INamedTypeSymbol typeSymbol, string interfaceFullName)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.ToDisplayString() == interfaceFullName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if a type has a public parameterless constructor.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type has a parameterless constructor.</returns>
    public static bool HasParameterlessConstructor(INamedTypeSymbol typeSymbol)
    {
        foreach (var ctor in typeSymbol.InstanceConstructors)
        {
            if (ctor.DeclaredAccessibility == Accessibility.Public &&
                ctor.Parameters.Length == 0)
            {
                return true;
            }
        }

        // If no explicit constructors, the default constructor is available
        // (unless there are other constructors with parameters)
        if (typeSymbol.InstanceConstructors.Length == 0)
            return true;

        return false;
    }

    /// <summary>
    /// Gets the attribute types applied to a plugin type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>A list of attribute type names (fully qualified).</returns>
    public static IReadOnlyList<string> GetPluginAttributes(INamedTypeSymbol typeSymbol)
    {
        var result = new List<string>();

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            // Skip system attributes and compiler-generated attributes
            var ns = attributeClass.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (ns.StartsWith("System.Runtime.CompilerServices", StringComparison.Ordinal))
                continue;

            // Include this attribute
            var attributeName = GetFullyQualifiedName(attributeClass);
            if (!result.Contains(attributeName))
            {
                result.Add(attributeName);
            }
        }

        // Also check for inherited attributes from base types
        var baseType = typeSymbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            foreach (var attribute in baseType.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass == null)
                    continue;

                // Check if attribute is inherited
                if (!IsInheritedAttribute(attributeClass))
                    continue;

                var ns = attributeClass.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                if (ns.StartsWith("System.Runtime.CompilerServices", StringComparison.Ordinal))
                    continue;

                var attributeName = GetFullyQualifiedName(attributeClass);
                if (!result.Contains(attributeName))
                {
                    result.Add(attributeName);
                }
            }

            baseType = baseType.BaseType;
        }

        return result;
    }

    /// <summary>
    /// Checks if an attribute type has [AttributeUsage(Inherited = true)].
    /// </summary>
    private static bool IsInheritedAttribute(INamedTypeSymbol attributeClass)
    {
        foreach (var attr in attributeClass.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != "System.AttributeUsageAttribute")
                continue;

            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "Inherited" && namedArg.Value.Value is bool inherited)
                {
                    return inherited;
                }
            }

            // Default for AttributeUsage is Inherited = true
            return true;
        }

        // Default is Inherited = true
        return true;
    }

    /// <summary>
    /// Gets the parameters of the best injectable constructor for a type.
    /// Returns the first constructor where all parameters are injectable types.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to analyze.</param>
    /// <returns>
    /// A list of fully qualified parameter type names, or null if no injectable constructor was found.
    /// </returns>
    public static IReadOnlyList<string>? GetBestConstructorParameters(INamedTypeSymbol typeSymbol)
    {
        foreach (var ctor in typeSymbol.InstanceConstructors)
        {
            if (ctor.IsStatic)
                continue;

            if (ctor.DeclaredAccessibility != Accessibility.Public)
                continue;

            var parameters = ctor.Parameters;

            // Parameterless constructor - no parameters needed
            if (parameters.Length == 0)
                return Array.Empty<string>();

            // Single parameter of same type (copy constructor) - skip
            if (parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(parameters[0].Type, typeSymbol))
                continue;

            // Check if all parameters are injectable
            if (!AllParametersAreInjectable(parameters))
                continue;

            // This constructor is good - collect parameter type names
            var parameterTypes = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypes[i] = GetFullyQualifiedNameForType(parameters[i].Type);
            }
            return parameterTypes;
        }

        return null;
    }

    /// <summary>
    /// Gets the fully qualified name for any type symbol (including generics like Lazy&lt;T&gt;).
    /// </summary>
    private static string GetFullyQualifiedNameForType(ITypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// Checks if a type has any methods with the [KernelFunction] attribute.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <param name="isCurrentAssembly">True if the type is from the current compilation's assembly (allows internal types).</param>
    /// <returns>True if the type has kernel function methods.</returns>
    public static bool HasKernelFunctions(INamedTypeSymbol typeSymbol, bool isCurrentAssembly = false)
    {
        const string KernelFunctionAttributeName = "Microsoft.SemanticKernel.KernelFunctionAttribute";

        // Must be a class (static or instance)
        if (typeSymbol.TypeKind != TypeKind.Class)
            return false;

        // Must be accessible from generated code
        if (!IsAccessibleFromGeneratedCode(typeSymbol, isCurrentAssembly))
            return false;

        // For non-static classes, must not be abstract
        if (!typeSymbol.IsStatic && typeSymbol.IsAbstract)
            return false;

        // Check for methods with [KernelFunction] attribute
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            // Skip constructors, property accessors, etc.
            if (method.MethodKind != MethodKind.Ordinary)
                continue;

            // Must be public
            if (method.DeclaredAccessibility != Accessibility.Public)
                continue;

            // Check for [KernelFunction] attribute
            foreach (var attribute in method.GetAttributes())
            {
                var attrName = attribute.AttributeClass?.ToDisplayString();
                if (attrName == KernelFunctionAttributeName)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Result of hub registration discovery.
    /// </summary>
    public readonly struct HubRegistrationInfo
    {
        public HubRegistrationInfo(string hubTypeName, string hubPath)
        {
            HubTypeName = hubTypeName;
            HubPath = hubPath;
        }

        public string HubTypeName { get; }
        public string HubPath { get; }
    }

    /// <summary>
    /// Tries to extract hub registration info from an IHubRegistrationPlugin implementation.
    /// Returns null if the type doesn't implement IHubRegistrationPlugin or if the values can't be determined at compile time.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <param name="compilation">The compilation context.</param>
    /// <param name="isCurrentAssembly">True if the type is from the current compilation's assembly (allows internal types).</param>
    /// <returns>Hub registration info if discoverable, null otherwise.</returns>
    public static HubRegistrationInfo? TryGetHubRegistrationInfo(INamedTypeSymbol typeSymbol, Compilation compilation, bool isCurrentAssembly = false)
    {
        const string HubRegistrationPluginInterfaceName = "NexusLabs.Needlr.SignalR.IHubRegistrationPlugin";

        // Check if type implements IHubRegistrationPlugin
        if (!ImplementsInterface(typeSymbol, HubRegistrationPluginInterfaceName))
            return null;

        // Must be accessible from generated code
        if (!IsAccessibleFromGeneratedCode(typeSymbol, isCurrentAssembly))
            return null;

        // Must have a parameterless constructor to be instantiable
        if (!HasParameterlessConstructor(typeSymbol))
            return null;

        // Try to find HubPath and HubType property values
        string? hubPath = null;
        string? hubTypeName = null;

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol property)
                continue;

            if (property.Name == "HubPath")
            {
                // Try to get the constant value from the property getter
                hubPath = TryGetPropertyStringValue(property, compilation);
            }
            else if (property.Name == "HubType")
            {
                // Try to get the type returned by the property
                hubTypeName = TryGetPropertyTypeValue(property, compilation);
            }
        }

        if (hubPath != null && hubTypeName != null)
        {
            return new HubRegistrationInfo(hubTypeName, hubPath);
        }

        return null;
    }

    private static string? TryGetPropertyStringValue(IPropertySymbol property, Compilation compilation)
    {
        // For expression-bodied properties like: string HubPath => "/chat";
        // Or get-only properties with a return statement
        var syntaxRefs = property.DeclaringSyntaxReferences;
        if (syntaxRefs.Length == 0)
            return null;

        var syntax = syntaxRefs[0].GetSyntax();

        // Look for string literal in the property
        foreach (var node in syntax.DescendantNodes())
        {
            if (node is Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax literal)
            {
                var semanticModel = compilation.GetSemanticModel(literal.SyntaxTree);
                var constantValue = semanticModel.GetConstantValue(literal);
                if (constantValue.HasValue && constantValue.Value is string strValue)
                {
                    return strValue;
                }
            }
        }

        return null;
    }

    private static string? TryGetPropertyTypeValue(IPropertySymbol property, Compilation compilation)
    {
        // For properties like: Type HubType => typeof(ChatHub);
        var syntaxRefs = property.DeclaringSyntaxReferences;
        if (syntaxRefs.Length == 0)
            return null;

        var syntax = syntaxRefs[0].GetSyntax();

        // Look for typeof expression in the property
        foreach (var node in syntax.DescendantNodes())
        {
            if (node is Microsoft.CodeAnalysis.CSharp.Syntax.TypeOfExpressionSyntax typeOfExpr)
            {
                var semanticModel = compilation.GetSemanticModel(typeOfExpr.SyntaxTree);
                var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
                if (typeInfo.Type is INamedTypeSymbol namedType)
                {
                    return GetFullyQualifiedName(namedType);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a type has the <c>[DeferToContainer]</c> attribute.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type has the DeferToContainer attribute.</returns>
    public static bool HasDeferToContainerAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;

            var name = attrClass.Name;
            var fullName = attrClass.ToDisplayString();

            if (name == DeferToContainerAttributeName || fullName == DeferToContainerAttributeFullName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the constructor parameter types declared in the <c>[DeferToContainer]</c> attribute.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>
    /// A list of fully qualified parameter type names from the attribute, 
    /// or null if the attribute is not present.
    /// </returns>
    public static IReadOnlyList<string>? GetDeferToContainerParameterTypes(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;

            var name = attrClass.Name;
            var fullName = attrClass.ToDisplayString();

            if (name != DeferToContainerAttributeName && fullName != DeferToContainerAttributeFullName)
                continue;

            // The attribute has a params Type[] constructor parameter
            // Check constructor arguments
            if (attribute.ConstructorArguments.Length == 0)
                return Array.Empty<string>();

            var arg = attribute.ConstructorArguments[0];
            
            // params array is passed as a single array argument
            if (arg.Kind == TypedConstantKind.Array)
            {
                var types = new List<string>();
                foreach (var element in arg.Values)
                {
                    if (element.Value is INamedTypeSymbol namedType)
                    {
                        types.Add(GetFullyQualifiedName(namedType));
                    }
                }
                return types;
            }
        }

        return null;
    }
}
