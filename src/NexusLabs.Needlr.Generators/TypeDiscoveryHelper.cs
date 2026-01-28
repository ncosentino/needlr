using Microsoft.CodeAnalysis;
using SharedHelper = NexusLabs.Needlr.Roslyn.Shared.TypeDiscoveryHelper;

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
    private const string DoNotInjectAttributeName = "DoNotInjectAttribute";
    private const string DoNotInjectAttributeFullName = "NexusLabs.Needlr.DoNotInjectAttribute";
    private const string DeferToContainerAttributeName = "DeferToContainerAttribute";
    private const string DeferToContainerAttributeFullName = "NexusLabs.Needlr.DeferToContainerAttribute";
    private const string DecoratorForAttributePrefix = "NexusLabs.Needlr.DecoratorForAttribute";
    private const string KeyedAttributeName = "KeyedAttribute";
    private const string KeyedAttributeFullName = "NexusLabs.Needlr.KeyedAttribute";
    private const string SingletonAttributeName = "SingletonAttribute";
    private const string SingletonAttributeFullName = "NexusLabs.Needlr.SingletonAttribute";
    private const string ScopedAttributeName = "ScopedAttribute";
    private const string ScopedAttributeFullName = "NexusLabs.Needlr.ScopedAttribute";
    private const string TransientAttributeName = "TransientAttribute";
    private const string TransientAttributeFullName = "NexusLabs.Needlr.TransientAttribute";
    private const string GenerateFactoryAttributeName = "GenerateFactoryAttribute";
    private const string GenerateFactoryAttributeFullName = "NexusLabs.Needlr.Generators.GenerateFactoryAttribute";
    private const string OptionsAttributeName = "OptionsAttribute";
    private const string OptionsAttributeFullName = "NexusLabs.Needlr.Generators.OptionsAttribute";
    private const string OptionsValidatorAttributeName = "OptionsValidatorAttribute";
    private const string OptionsValidatorAttributeFullName = "NexusLabs.Needlr.Generators.OptionsValidatorAttribute";

    /// <summary>
    /// Determines whether a type symbol represents a concrete injectable type.
    /// Delegates to the shared library for consistency with analyzers.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <param name="isCurrentAssembly">True if the type is from the current compilation's assembly (allows internal types).</param>
    /// <returns>True if the type is a valid injectable type; otherwise, false.</returns>
    public static bool IsInjectableType(INamedTypeSymbol typeSymbol, bool isCurrentAssembly = false)
        => SharedHelper.IsInjectableType(typeSymbol, isCurrentAssembly);

    private const string RegisterAsAttributePrefix = "NexusLabs.Needlr.RegisterAsAttribute";

    /// <summary>
    /// Gets the interfaces that should be registered for a type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to get interfaces for.</param>
    /// <returns>A list of interface symbols suitable for registration.</returns>
    public static IReadOnlyList<INamedTypeSymbol> GetRegisterableInterfaces(INamedTypeSymbol typeSymbol)
    {
        // Check for [RegisterAs<T>] attributes - if present, only register as those interfaces
        var registerAsInterfaces = GetRegisterAsInterfaces(typeSymbol);
        if (registerAsInterfaces.Count > 0)
        {
            return registerAsInterfaces;
        }

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
    /// Gets interface types specified by [RegisterAs&lt;T&gt;] attributes on the type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>A list of interface symbols from RegisterAs attributes.</returns>
    public static IReadOnlyList<INamedTypeSymbol> GetRegisterAsInterfaces(INamedTypeSymbol typeSymbol)
    {
        var result = new List<INamedTypeSymbol>();

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass == null)
                continue;

            // Check for RegisterAsAttribute<T>
            var attrFullName = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (!attrFullName.StartsWith("global::" + RegisterAsAttributePrefix, StringComparison.Ordinal))
                continue;

            // Get the type argument
            if (attrClass.IsGenericType && attrClass.TypeArguments.Length == 1)
            {
                if (attrClass.TypeArguments[0] is INamedTypeSymbol interfaceType)
                {
                    result.Add(interfaceType);
                }
            }
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
        => SharedHelper.HasDoNotAutoRegisterAttribute(typeSymbol);

    private static bool HasDoNotAutoRegisterAttributeDirect(INamedTypeSymbol typeSymbol)
        => SharedHelper.HasDoNotAutoRegisterAttributeDirect(typeSymbol);

    private static bool IsCompilerGenerated(INamedTypeSymbol typeSymbol)
        => SharedHelper.IsCompilerGenerated(typeSymbol);

    private static bool InheritsFrom(INamedTypeSymbol typeSymbol, string baseTypeName)
        => SharedHelper.InheritsFrom(typeSymbol, baseTypeName);

    private static bool IsSystemInterface(INamedTypeSymbol interfaceSymbol)
        => SharedHelper.IsSystemType(interfaceSymbol);

    private static bool IsSystemType(INamedTypeSymbol typeSymbol)
        => SharedHelper.IsSystemType(typeSymbol);

    private static bool HasUnsatisfiedRequiredMembers(INamedTypeSymbol typeSymbol)
        => SharedHelper.HasUnsatisfiedRequiredMembers(typeSymbol);

    /// <summary>
    /// Checks if a type is accessible from generated code.
    /// For types in the current assembly, internal and public types are accessible.
    /// For types in referenced assemblies, only public types are accessible.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <param name="isCurrentAssembly">True if the type is from the current compilation's assembly.</param>
    /// <returns>True if the type is accessible from generated code.</returns>
    private static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol typeSymbol, bool isCurrentAssembly)
        => SharedHelper.IsAccessibleFromGeneratedCode(typeSymbol, isCurrentAssembly);

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

        // NOTE: Records ARE allowed as plugins (they are classes with parameterless constructors).
        // Records are excluded from IsInjectableType (auto-registration) but not from plugin discovery.

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
    /// Determines the injectable lifetime for a type by analyzing its attributes and constructors.
    /// Checks for explicit lifetime attributes first, then falls back to Singleton if injectable.
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
            return GetExplicitLifetime(typeSymbol) ?? GeneratorLifetime.Singleton;

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
                return GetExplicitLifetime(typeSymbol) ?? GeneratorLifetime.Singleton;

            // Single parameter of same type (copy constructor) - not injectable
            if (parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(parameters[0].Type, typeSymbol))
                continue;

            // Check if all parameters are injectable types
            if (AllParametersAreInjectable(parameters))
                return GetExplicitLifetime(typeSymbol) ?? GeneratorLifetime.Singleton;
        }

        return null;
    }

    /// <summary>
    /// Gets the explicit lifetime from attributes if specified.
    /// </summary>
    private static GeneratorLifetime? GetExplicitLifetime(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            var name = attributeClass.Name;
            var fullName = attributeClass.ToDisplayString();

            if (name == TransientAttributeName || fullName == TransientAttributeFullName)
                return GeneratorLifetime.Transient;

            if (name == ScopedAttributeName || fullName == ScopedAttributeFullName)
                return GeneratorLifetime.Scoped;

            if (name == SingletonAttributeName || fullName == SingletonAttributeFullName)
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

        // NOTE: Records ARE allowed as plugins (they are classes with parameterless constructors).
        // Records are excluded from IsInjectableType (auto-registration) but not from plugin discovery.
        // Use case: CacheConfiguration records can be discovered via IPluginFactory.CreatePluginsFromAssemblies<T>()

        // Must have a parameterless constructor
        if (!HasParameterlessConstructor(typeSymbol))
            return false;

        // Exclude types with required members that can't be set via constructor
        // These would cause compilation errors: "Required member 'X' must be set"
        if (HasUnsatisfiedRequiredMembers(typeSymbol))
            return false;

        return true;
    }

    /// <summary>
    /// Gets the plugin base types (interfaces and base classes) for a type.
    /// Plugin base types are non-System interfaces and non-System/non-object base classes.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>A list of plugin base type symbols (interfaces and base classes).</returns>
    public static IReadOnlyList<INamedTypeSymbol> GetPluginInterfaces(INamedTypeSymbol typeSymbol)
    {
        var result = new List<INamedTypeSymbol>();

        // Add non-system interfaces
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.IsUnboundGenericType)
                continue;

            if (IsSystemInterface(iface))
                continue;

            result.Add(iface);
        }

        // Add non-system base classes (walking up the hierarchy)
        var baseType = typeSymbol.BaseType;
        while (baseType != null)
        {
            // Stop at System.Object or System types
            if (IsSystemType(baseType))
                break;

            // Skip abstract types that can't be instantiated directly
            // but include them as they represent the plugin contract
            result.Add(baseType);
            baseType = baseType.BaseType;
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
    /// Represents a constructor parameter with optional keyed service information.
    /// </summary>
    public readonly struct ConstructorParameterInfo
    {
        public ConstructorParameterInfo(string typeName, string? serviceKey = null, string? parameterName = null, string? documentationComment = null)
        {
            TypeName = typeName;
            ServiceKey = serviceKey;
            ParameterName = parameterName;
            DocumentationComment = documentationComment;
        }

        /// <summary>
        /// The fully qualified type name of the parameter.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// The service key from [FromKeyedServices] attribute, or null if not a keyed service.
        /// </summary>
        public string? ServiceKey { get; }

        /// <summary>
        /// The original parameter name from the constructor (used for factory generation).
        /// </summary>
        public string? ParameterName { get; }

        /// <summary>
        /// XML documentation comment for this parameter, extracted from the constructor's XML docs.
        /// </summary>
        public string? DocumentationComment { get; }

        /// <summary>
        /// True if this parameter should be resolved as a keyed service.
        /// </summary>
        public bool IsKeyed => ServiceKey is not null;
    }

    /// <summary>
    /// Gets the parameters of the best injectable constructor for a type, including keyed service info.
    /// Returns the first constructor where all parameters are injectable types.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to analyze.</param>
    /// <returns>
    /// A list of constructor parameter info, or null if no injectable constructor was found.
    /// </returns>
    public static IReadOnlyList<ConstructorParameterInfo>? GetBestConstructorParametersWithKeys(INamedTypeSymbol typeSymbol)
    {
        const string FromKeyedServicesAttributeName = "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute";

        foreach (var ctor in typeSymbol.InstanceConstructors)
        {
            if (ctor.IsStatic)
                continue;

            if (ctor.DeclaredAccessibility != Accessibility.Public)
                continue;

            var parameters = ctor.Parameters;

            // Parameterless constructor - no parameters needed
            if (parameters.Length == 0)
                return Array.Empty<ConstructorParameterInfo>();

            // Single parameter of same type (copy constructor) - skip
            if (parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(parameters[0].Type, typeSymbol))
                continue;

            // Check if all parameters are injectable
            if (!AllParametersAreInjectable(parameters))
                continue;

            // This constructor is good - collect parameter info with keyed service keys
            var parameterInfos = new ConstructorParameterInfo[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var typeName = GetFullyQualifiedNameForType(param.Type);
                string? serviceKey = null;

                // Check for [FromKeyedServices("key")] attribute
                foreach (var attr in param.GetAttributes())
                {
                    var attrClass = attr.AttributeClass;
                    if (attrClass is null)
                        continue;

                    var attrFullName = attrClass.ToDisplayString();
                    if (attrFullName == FromKeyedServicesAttributeName)
                    {
                        // Extract the key from the constructor argument
                        if (attr.ConstructorArguments.Length > 0)
                        {
                            var keyArg = attr.ConstructorArguments[0];
                            if (keyArg.Value is string keyValue)
                            {
                                serviceKey = keyValue;
                            }
                        }
                        break;
                    }
                }

                parameterInfos[i] = new ConstructorParameterInfo(typeName, serviceKey);
            }
            return parameterInfos;
        }

        return null;
    }

    /// <summary>
    /// Gets the service keys from [Keyed] attributes on a type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>Array of service keys, or empty array if no [Keyed] attributes found.</returns>
    public static string[] GetKeyedServiceKeys(INamedTypeSymbol typeSymbol)
    {
        var keys = new List<string>();

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            var name = attributeClass.Name;
            var fullName = attributeClass.ToDisplayString();

            if (name == KeyedAttributeName || fullName == KeyedAttributeFullName)
            {
                // Extract the key from the constructor argument
                if (attribute.ConstructorArguments.Length > 0)
                {
                    var keyArg = attribute.ConstructorArguments[0];
                    if (keyArg.Value is string keyValue)
                    {
                        keys.Add(keyValue);
                    }
                }
            }
        }

        return keys.ToArray();
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

    /// <summary>
    /// Result of decorator discovery.
    /// </summary>
    public readonly struct DecoratorInfo
    {
        public DecoratorInfo(string decoratorTypeName, string serviceTypeName, int order)
        {
            DecoratorTypeName = decoratorTypeName;
            ServiceTypeName = serviceTypeName;
            Order = order;
        }

        public string DecoratorTypeName { get; }
        public string ServiceTypeName { get; }
        public int Order { get; }
    }

    /// <summary>
    /// Gets all DecoratorFor&lt;T&gt; attributes applied to a type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>A list of decorator info for each DecoratorFor attribute found.</returns>
    public static IReadOnlyList<DecoratorInfo> GetDecoratorForAttributes(INamedTypeSymbol typeSymbol)
    {
        var result = new List<DecoratorInfo>();

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;

            // Check if this is a generic DecoratorForAttribute<T>
            if (!attrClass.IsGenericType)
                continue;

            var unboundTypeName = attrClass.ConstructedFrom?.ToDisplayString();
            if (unboundTypeName is null || !unboundTypeName.StartsWith(DecoratorForAttributePrefix, StringComparison.Ordinal))
                continue;

            // Get the service type from the generic type argument
            if (attrClass.TypeArguments.Length != 1)
                continue;

            var serviceType = attrClass.TypeArguments[0] as INamedTypeSymbol;
            if (serviceType is null)
                continue;

            var serviceTypeName = GetFullyQualifiedName(serviceType);
            var decoratorTypeName = GetFullyQualifiedName(typeSymbol);

            // Get the Order property value
            int order = 0;
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == "Order" && namedArg.Value.Value is int orderValue)
                {
                    order = orderValue;
                    break;
                }
            }

            result.Add(new DecoratorInfo(decoratorTypeName, serviceTypeName, order));
        }

        return result;
    }

    /// <summary>
    /// Checks if a type has any DecoratorFor&lt;T&gt; attributes.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type has at least one DecoratorFor attribute.</returns>
    public static bool HasDecoratorForAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;

            if (!attrClass.IsGenericType)
                continue;

            var unboundTypeName = attrClass.ConstructedFrom?.ToDisplayString();
            if (unboundTypeName is not null && unboundTypeName.StartsWith(DecoratorForAttributePrefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    #region Open Generic Decorator Discovery

    private const string OpenDecoratorForAttributeName = "OpenDecoratorForAttribute";
    private const string OpenDecoratorForAttributeFullName = "NexusLabs.Needlr.Generators.OpenDecoratorForAttribute";

    /// <summary>
    /// Result of open generic decorator discovery.
    /// </summary>
    public readonly struct OpenDecoratorInfo
    {
        public OpenDecoratorInfo(
            INamedTypeSymbol decoratorType,
            INamedTypeSymbol openGenericInterface,
            int order)
        {
            DecoratorType = decoratorType;
            OpenGenericInterface = openGenericInterface;
            Order = order;
        }

        /// <summary>The open generic decorator type symbol (e.g., LoggingDecorator{T}).</summary>
        public INamedTypeSymbol DecoratorType { get; }

        /// <summary>The open generic interface being decorated (e.g., IHandler{T}).</summary>
        public INamedTypeSymbol OpenGenericInterface { get; }

        /// <summary>Order in which decorator wraps (lower = outer).</summary>
        public int Order { get; }
    }

    /// <summary>
    /// Gets all OpenDecoratorFor attributes applied to a type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>A list of open decorator info for each OpenDecoratorFor attribute found.</returns>
    public static IReadOnlyList<OpenDecoratorInfo> GetOpenDecoratorForAttributes(INamedTypeSymbol typeSymbol)
    {
        var result = new List<OpenDecoratorInfo>();

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;

            // Check if this is OpenDecoratorForAttribute
            if (attrClass.Name != OpenDecoratorForAttributeName)
                continue;

            var fullName = attrClass.ToDisplayString();
            if (fullName != OpenDecoratorForAttributeFullName)
                continue;

            // Get the type argument from the constructor (first positional argument)
            if (attribute.ConstructorArguments.Length < 1)
                continue;

            var typeArg = attribute.ConstructorArguments[0];
            if (typeArg.Value is not INamedTypeSymbol openGenericInterface)
                continue;

            // Validate it's an open generic interface
            if (!openGenericInterface.IsUnboundGenericType || openGenericInterface.TypeKind != TypeKind.Interface)
                continue;

            // Get the Order property value
            int order = 0;
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == "Order" && namedArg.Value.Value is int orderValue)
                {
                    order = orderValue;
                    break;
                }
            }

            result.Add(new OpenDecoratorInfo(typeSymbol, openGenericInterface, order));
        }

        return result;
    }

    /// <summary>
    /// Checks if a type has any OpenDecoratorFor attributes.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type has at least one OpenDecoratorFor attribute.</returns>
    public static bool HasOpenDecoratorForAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;

            if (attrClass.Name == OpenDecoratorForAttributeName)
            {
                var fullName = attrClass.ToDisplayString();
                if (fullName == OpenDecoratorForAttributeFullName)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds all closed implementations of an open generic interface in the given types.
    /// </summary>
    /// <param name="openGenericInterface">The open generic interface (e.g., IHandler{}).</param>
    /// <param name="allTypes">All types to search through.</param>
    /// <returns>A dictionary mapping closed interface types to their implementing types.</returns>
    public static Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> FindClosedImplementations(
        INamedTypeSymbol openGenericInterface,
        IEnumerable<INamedTypeSymbol> allTypes)
    {
        var result = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var type in allTypes)
        {
            if (type.IsAbstract || type.TypeKind != TypeKind.Class)
                continue;

            // Check all interfaces implemented by this type
            foreach (var iface in type.AllInterfaces)
            {
                // Check if this interface is a closed version of the open generic
                if (iface.OriginalDefinition.Equals(openGenericInterface, SymbolEqualityComparer.Default))
                {
                    if (!result.TryGetValue(iface, out var implementors))
                    {
                        implementors = new List<INamedTypeSymbol>();
                        result[iface] = implementors;
                    }
                    implementors.Add(type);
                }
            }
        }

        return result;
    }

    #endregion

    #region Interceptor Discovery

    private const string InterceptAttributePrefix = "NexusLabs.Needlr.InterceptAttribute";


    /// <summary>
    /// Result of interceptor discovery for a type.
    /// </summary>
    public readonly struct InterceptorInfo
    {
        public InterceptorInfo(
            string interceptorTypeName,
            int order,
            bool isMethodLevel,
            string? methodName)
        {
            InterceptorTypeName = interceptorTypeName;
            Order = order;
            IsMethodLevel = isMethodLevel;
            MethodName = methodName;
        }

        /// <summary>Fully qualified name of the interceptor type.</summary>
        public string InterceptorTypeName { get; }

        /// <summary>Order in which interceptor executes (lower = first).</summary>
        public int Order { get; }

        /// <summary>True if this interceptor is applied at method level, false for class level.</summary>
        public bool IsMethodLevel { get; }

        /// <summary>Method name if IsMethodLevel is true, null otherwise.</summary>
        public string? MethodName { get; }
    }

    /// <summary>
    /// Gets all Intercept attributes applied to a type (class-level only).
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>A list of interceptor info for each Intercept attribute found.</returns>
    public static IReadOnlyList<InterceptorInfo> GetInterceptAttributes(INamedTypeSymbol typeSymbol)
    {
        var result = new List<InterceptorInfo>();

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var interceptorType = TryGetInterceptorType(attribute);
            if (interceptorType is null)
                continue;

            var order = GetInterceptorOrder(attribute);
            var interceptorTypeName = GetFullyQualifiedName(interceptorType);

            result.Add(new InterceptorInfo(interceptorTypeName, order, isMethodLevel: false, methodName: null));
        }

        return result;
    }

    /// <summary>
    /// Gets all Intercept attributes applied to methods of a type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>A list of interceptor info for each method-level Intercept attribute found.</returns>
    public static IReadOnlyList<InterceptorInfo> GetMethodLevelInterceptAttributes(INamedTypeSymbol typeSymbol)
    {
        var result = new List<InterceptorInfo>();

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IMethodSymbol methodSymbol)
                continue;

            if (methodSymbol.MethodKind != MethodKind.Ordinary)
                continue;

            foreach (var attribute in methodSymbol.GetAttributes())
            {
                var interceptorType = TryGetInterceptorType(attribute);
                if (interceptorType is null)
                    continue;

                var order = GetInterceptorOrder(attribute);
                var interceptorTypeName = GetFullyQualifiedName(interceptorType);

                result.Add(new InterceptorInfo(interceptorTypeName, order, isMethodLevel: true, methodName: methodSymbol.Name));
            }
        }

        return result;
    }

    /// <summary>
    /// Checks if a type has any Intercept attributes (class or method level).
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type has at least one Intercept attribute.</returns>
    public static bool HasInterceptAttributes(INamedTypeSymbol typeSymbol)
    {
        // Check class-level
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (TryGetInterceptorType(attribute) is not null)
                return true;
        }

        // Check method-level
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IMethodSymbol methodSymbol)
                continue;

            foreach (var attribute in methodSymbol.GetAttributes())
            {
                if (TryGetInterceptorType(attribute) is not null)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the interface methods that need to be proxied for interception.
    /// </summary>
    /// <param name="typeSymbol">The type symbol implementing the interface(s).</param>
    /// <param name="classLevelInterceptors">Interceptors applied at class level.</param>
    /// <param name="methodLevelInterceptors">Interceptors applied at method level.</param>
    /// <returns>A list of method infos that need to be generated in the proxy.</returns>
    public static IReadOnlyList<InterceptedMethodInfo> GetInterceptedMethods(
        INamedTypeSymbol typeSymbol,
        IReadOnlyList<InterceptorInfo> classLevelInterceptors,
        IReadOnlyList<InterceptorInfo> methodLevelInterceptors)
    {
        var result = new List<InterceptedMethodInfo>();
        var methodNameToInterceptors = methodLevelInterceptors
            .GroupBy(i => i.MethodName!)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (IsSystemInterface(iface))
                continue;

            foreach (var member in iface.GetMembers())
            {
                if (member is not IMethodSymbol methodSymbol)
                    continue;

                if (methodSymbol.MethodKind != MethodKind.Ordinary)
                    continue;

                // Combine class-level and method-level interceptors for this method
                var interceptors = new List<InterceptorInfo>(classLevelInterceptors);
                if (methodNameToInterceptors.TryGetValue(methodSymbol.Name, out var methodInterceptors))
                {
                    interceptors.AddRange(methodInterceptors);
                }

                // Always include the method - even if no interceptors, the proxy needs to forward the call
                var methodInfo = new InterceptedMethodInfo(
                    methodSymbol.Name,
                    GetMethodReturnType(methodSymbol),
                    GetMethodParameters(methodSymbol),
                    GetFullyQualifiedName(iface),
                    methodSymbol.IsAsync || IsTaskOrValueTask(methodSymbol.ReturnType),
                    methodSymbol.ReturnsVoid,
                    interceptors.OrderBy(i => i.Order).Select(i => i.InterceptorTypeName).ToArray());

                result.Add(methodInfo);
            }
        }

        return result;
    }

    private static INamedTypeSymbol? TryGetInterceptorType(AttributeData attribute)
    {
        var attrClass = attribute.AttributeClass;
        if (attrClass is null)
            return null;

        // Check generic InterceptAttribute<T>
        if (attrClass.IsGenericType)
        {
            var unboundTypeName = attrClass.ConstructedFrom?.ToDisplayString();
            if (unboundTypeName is not null && unboundTypeName.StartsWith(InterceptAttributePrefix, StringComparison.Ordinal))
            {
                if (attrClass.TypeArguments.Length == 1 && attrClass.TypeArguments[0] is INamedTypeSymbol interceptorType)
                {
                    return interceptorType;
                }
            }
        }

        // Check non-generic InterceptAttribute(typeof(T))
        var attrClassName = attrClass.ToDisplayString();
        if (attrClassName == InterceptAttributePrefix)
        {
            // Get from constructor argument
            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is INamedTypeSymbol ctorInterceptorType)
            {
                return ctorInterceptorType;
            }
        }

        return null;
    }

    private static int GetInterceptorOrder(AttributeData attribute)
    {
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == "Order" && namedArg.Value.Value is int orderValue)
            {
                return orderValue;
            }
        }
        return 0;
    }

    private static string GetMethodReturnType(IMethodSymbol methodSymbol)
    {
        return methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static IReadOnlyList<MethodParameterInfo> GetMethodParameters(IMethodSymbol methodSymbol)
    {
        var result = new List<MethodParameterInfo>();
        foreach (var param in methodSymbol.Parameters)
        {
            result.Add(new MethodParameterInfo(
                param.Name,
                param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                param.RefKind));
        }
        return result;
    }

    private static bool IsTaskOrValueTask(ITypeSymbol typeSymbol)
    {
        var name = typeSymbol.ToDisplayString();
        return name.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal) ||
               name.StartsWith("System.Threading.Tasks.ValueTask", StringComparison.Ordinal);
    }

    /// <summary>
    /// Represents a method parameter for code generation.
    /// </summary>
    public readonly struct MethodParameterInfo
    {
        public MethodParameterInfo(string name, string typeName, RefKind refKind)
        {
            Name = name;
            TypeName = typeName;
            RefKind = refKind;
        }

        public string Name { get; }
        public string TypeName { get; }
        public RefKind RefKind { get; }

        public string GetDeclaration()
        {
            var refPrefix = RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                RefKind.RefReadOnlyParameter => "ref readonly ",
                _ => ""
            };
            return $"{refPrefix}{TypeName} {Name}";
        }
    }

    /// <summary>
    /// Represents a method that needs to be intercepted.
    /// </summary>
    public readonly struct InterceptedMethodInfo
    {
        public InterceptedMethodInfo(
            string name,
            string returnType,
            IReadOnlyList<MethodParameterInfo> parameters,
            string interfaceTypeName,
            bool isAsync,
            bool isVoid,
            string[] interceptorTypeNames)
        {
            Name = name;
            ReturnType = returnType;
            Parameters = parameters;
            InterfaceTypeName = interfaceTypeName;
            IsAsync = isAsync;
            IsVoid = isVoid;
            InterceptorTypeNames = interceptorTypeNames;
        }

        public string Name { get; }
        public string ReturnType { get; }
        public IReadOnlyList<MethodParameterInfo> Parameters { get; }
        public string InterfaceTypeName { get; }
        public bool IsAsync { get; }
        public bool IsVoid { get; }
        public string[] InterceptorTypeNames { get; }

        public string GetParameterList()
        {
            return string.Join(", ", Parameters.Select(p => p.GetDeclaration()));
        }

        public string GetArgumentList()
        {
            return string.Join(", ", Parameters.Select(p =>
            {
                var prefix = p.RefKind switch
                {
                    RefKind.Ref => "ref ",
                    RefKind.Out => "out ",
                    RefKind.In => "in ",
                    _ => ""
                };
                return prefix + p.Name;
            }));
        }
    }

    #endregion

    #region Factory Generation

    /// <summary>
    /// Checks if a type has the [GenerateFactory] attribute.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type has [GenerateFactory]; otherwise, false.</returns>
    public static bool HasGenerateFactoryAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            // Check for non-generic GenerateFactoryAttribute
            var name = attributeClass.Name;
            if (name == GenerateFactoryAttributeName)
                return true;

            var fullName = attributeClass.ToDisplayString();
            if (fullName == GenerateFactoryAttributeFullName)
                return true;

            // Check for generic GenerateFactoryAttribute<T>
            if (attributeClass.IsGenericType)
            {
                var originalDef = attributeClass.OriginalDefinition;
                if (originalDef.Name == GenerateFactoryAttributeName || 
                    originalDef.ToDisplayString().StartsWith(GenerateFactoryAttributeFullName + "<"))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the factory generation mode from the [GenerateFactory] attribute.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>The Mode value (1=Func, 2=Interface, 3=All), or 3 (All) if not specified.</returns>
    public static int GetFactoryGenerationMode(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            var name = attributeClass.Name;
            var fullName = attributeClass.ToDisplayString();

            bool isFactoryAttribute = name == GenerateFactoryAttributeName || 
                                      fullName == GenerateFactoryAttributeFullName ||
                                      (attributeClass.IsGenericType && 
                                       attributeClass.OriginalDefinition.Name == GenerateFactoryAttributeName);

            if (isFactoryAttribute)
            {
                // Check named argument "Mode"
                foreach (var namedArg in attribute.NamedArguments)
                {
                    if (namedArg.Key == "Mode" && namedArg.Value.Value is int modeValue)
                    {
                        return modeValue;
                    }
                }
            }
        }

        return 3; // Default is All (Func | Interface)
    }

    /// <summary>
    /// Gets the interface type from a generic [GenerateFactory&lt;T&gt;] attribute, if present.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>The fully qualified interface type name, or null if non-generic attribute is used.</returns>
    public static string? GetFactoryReturnInterfaceType(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            // Check for generic GenerateFactoryAttribute<T>
            if (attributeClass.IsGenericType)
            {
                var originalDef = attributeClass.OriginalDefinition;
                if (originalDef.Name == GenerateFactoryAttributeName)
                {
                    // Extract the type argument
                    var typeArg = attributeClass.TypeArguments.FirstOrDefault();
                    if (typeArg != null)
                    {
                        return $"global::{typeArg.ToDisplayString()}";
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Partitions constructor parameters into injectable (DI-resolvable) and runtime (must be provided).
    /// </summary>
    /// <param name="typeSymbol">The type symbol to analyze.</param>
    /// <returns>A list of factory constructor infos, one for each viable constructor.</returns>
    public static IReadOnlyList<FactoryConstructorInfo> GetFactoryConstructors(INamedTypeSymbol typeSymbol)
    {
        var result = new List<FactoryConstructorInfo>();

        foreach (var ctor in typeSymbol.InstanceConstructors)
        {
            if (ctor.IsStatic)
                continue;

            if (ctor.DeclaredAccessibility != Accessibility.Public)
                continue;

            // Extract XML documentation for parameters
            var paramDocs = GetConstructorParameterDocumentation(ctor);

            var injectableParams = new List<ConstructorParameterInfo>();
            var runtimeParams = new List<ConstructorParameterInfo>();

            foreach (var param in ctor.Parameters)
            {
                var typeName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                paramDocs.TryGetValue(param.Name, out var docComment);

                if (IsInjectableParameterType(param.Type))
                {
                    // Check for [FromKeyedServices] attribute
                    var serviceKey = GetFromKeyedServicesKey(param);
                    var paramInfo = new ConstructorParameterInfo(typeName, serviceKey, param.Name, docComment);
                    injectableParams.Add(paramInfo);
                }
                else
                {
                    var paramInfo = new ConstructorParameterInfo(typeName, null, param.Name, docComment);
                    runtimeParams.Add(paramInfo);
                }
            }

            // Only include constructors that have at least one runtime parameter
            // (otherwise normal registration works fine)
            if (runtimeParams.Count > 0)
            {
                result.Add(new FactoryConstructorInfo(
                    injectableParams.ToArray(),
                    runtimeParams.ToArray()));
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts parameter documentation from a constructor's XML documentation comments.
    /// </summary>
    private static Dictionary<string, string> GetConstructorParameterDocumentation(IMethodSymbol constructor)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        var xmlDoc = constructor.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xmlDoc))
            return result;

        try
        {
            // Parse the XML documentation
            var doc = System.Xml.Linq.XDocument.Parse(xmlDoc);
            var paramElements = doc.Descendants("param");

            foreach (var param in paramElements)
            {
                var nameAttr = param.Attribute("name");
                if (nameAttr is null || string.IsNullOrWhiteSpace(nameAttr.Value))
                    continue;

                // Get the inner text/content of the param element
                var content = param.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    result[nameAttr.Value] = content!;
                }
            }
        }
        catch
        {
            // If XML parsing fails, return empty dictionary
        }

        return result;
    }

    private static string? GetFromKeyedServicesKey(IParameterSymbol param)
    {
        const string FromKeyedServicesAttributeName = "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute";

        foreach (var attr in param.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null)
                continue;

            var attrFullName = attrClass.ToDisplayString();
            if (attrFullName == FromKeyedServicesAttributeName)
            {
                // Extract the key from the constructor argument
                if (attr.ConstructorArguments.Length > 0)
                {
                    var keyArg = attr.ConstructorArguments[0];
                    if (keyArg.Value is string keyValue)
                    {
                        return keyValue;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Represents a constructor suitable for factory generation.
    /// </summary>
    public readonly struct FactoryConstructorInfo
    {
        public FactoryConstructorInfo(
            ConstructorParameterInfo[] injectableParameters,
            ConstructorParameterInfo[] runtimeParameters)
        {
            InjectableParameters = injectableParameters;
            RuntimeParameters = runtimeParameters;
        }

        /// <summary>Parameters that can be resolved from the service provider.</summary>
        public ConstructorParameterInfo[] InjectableParameters { get; }

        /// <summary>Parameters that must be provided at factory call time.</summary>
        public ConstructorParameterInfo[] RuntimeParameters { get; }
    }

    #endregion

    #region Plugin Order Discovery

    private const string PluginOrderAttributeName = "PluginOrderAttribute";
    private const string PluginOrderAttributeFullName = "NexusLabs.Needlr.PluginOrderAttribute";

    /// <summary>
    /// Gets the execution order for a plugin type from the [PluginOrder] attribute.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>The order value from [PluginOrder], or 0 if not specified.</returns>
    public static int GetPluginOrder(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            var name = attributeClass.Name;
            var fullName = attributeClass.ToDisplayString();

            if (name == PluginOrderAttributeName || fullName == PluginOrderAttributeFullName)
            {
                // The attribute has a single constructor parameter: int order
                if (attribute.ConstructorArguments.Length > 0 &&
                    attribute.ConstructorArguments[0].Value is int order)
                {
                    return order;
                }
            }
        }

        return 0; // Default order
    }

    #endregion

    #region Options Attribute Detection

    /// <summary>
    /// Information extracted from an [Options] attribute.
    /// </summary>
    public readonly struct OptionsAttributeInfo
    {
        public OptionsAttributeInfo(string? sectionName, string? name, bool validateOnStart, string? validateMethod = null, INamedTypeSymbol? validatorType = null)
        {
            SectionName = sectionName;
            Name = name;
            ValidateOnStart = validateOnStart;
            ValidateMethod = validateMethod;
            ValidatorType = validatorType;
        }

        /// <summary>Explicit section name from attribute, or null to infer from class name.</summary>
        public string? SectionName { get; }

        /// <summary>Named options name (e.g., "Primary"), or null for default options.</summary>
        public string? Name { get; }

        /// <summary>Whether to validate options on startup.</summary>
        public bool ValidateOnStart { get; }

        /// <summary>Custom validation method name, or null to use convention ("Validate").</summary>
        public string? ValidateMethod { get; }

        /// <summary>External validator type, or null to use the options class itself.</summary>
        public INamedTypeSymbol? ValidatorType { get; }
    }

    /// <summary>
    /// Checks if a type has the [Options] attribute.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type has [Options]; otherwise, false.</returns>
    public static bool HasOptionsAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            var name = attributeClass.Name;
            if (name == OptionsAttributeName)
                return true;

            var fullName = attributeClass.ToDisplayString();
            if (fullName == OptionsAttributeFullName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all [Options] attribute data from a type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>A list of options attribute info for each [Options] on the type.</returns>
    public static IReadOnlyList<OptionsAttributeInfo> GetOptionsAttributes(INamedTypeSymbol typeSymbol)
    {
        var result = new List<OptionsAttributeInfo>();

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            var name = attributeClass.Name;
            var fullName = attributeClass.ToDisplayString();

            if (name != OptionsAttributeName && fullName != OptionsAttributeFullName)
                continue;

            // Extract constructor argument (optional section name)
            string? sectionName = null;
            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string section)
            {
                sectionName = section;
            }

            // Extract named arguments
            string? optionsName = null;
            bool validateOnStart = false;
            string? validateMethod = null;
            INamedTypeSymbol? validatorType = null;

            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == "Name" && namedArg.Value.Value is string n)
                {
                    optionsName = n;
                }
                else if (namedArg.Key == "ValidateOnStart" && namedArg.Value.Value is bool v)
                {
                    validateOnStart = v;
                }
                else if (namedArg.Key == "ValidateMethod" && namedArg.Value.Value is string vm)
                {
                    validateMethod = vm;
                }
                else if (namedArg.Key == "Validator" && namedArg.Value.Value is INamedTypeSymbol vt)
                {
                    validatorType = vt;
                }
            }

            result.Add(new OptionsAttributeInfo(sectionName, optionsName, validateOnStart, validateMethod, validatorType));
        }

        return result;
    }

    /// <summary>
    /// Finds a validation method on a type by convention or explicit name.
    /// Looks for: 1) [OptionsValidator] attribute (legacy), 2) method by name (convention or explicit).
    /// </summary>
    /// <param name="typeSymbol">The type symbol to search.</param>
    /// <param name="methodName">The method name to look for (default: "Validate").</param>
    /// <returns>Validator method info, or null if no validator method found.</returns>
    public static OptionsValidatorMethodInfo? FindValidationMethod(INamedTypeSymbol typeSymbol, string methodName = "Validate")
    {
        // First, check for legacy [OptionsValidator] attribute
        var legacyMethod = GetOptionsValidatorMethod(typeSymbol);
        if (legacyMethod.HasValue)
            return legacyMethod;

        // Then, look for method by name (convention-based)
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            if (method.Name != methodName)
                continue;

            // Check signature: should return IEnumerable<string> or IEnumerable<ValidationError>
            // Supported signatures:
            // 1. Instance method with no parameters: T.Validate() - for self-validation
            // 2. Static method with one parameter: static T.Validate(TOptions options)
            // 3. Instance method with one parameter: validator.Validate(TOptions options) - for external validators
            if (method.Parameters.Length == 0 && !method.IsStatic)
            {
                // Instance method: T.Validate() - self-validation on options class
                return new OptionsValidatorMethodInfo(method.Name, false);
            }
            
            if (method.Parameters.Length == 1 && method.IsStatic)
            {
                // Static method: T.Validate(T options) - static validator
                return new OptionsValidatorMethodInfo(method.Name, true);
            }

            if (method.Parameters.Length == 1 && !method.IsStatic)
            {
                // Instance method with parameter: validator.Validate(T options) - external validator
                return new OptionsValidatorMethodInfo(method.Name, false);
            }
        }

        return null;
    }

    /// <summary>
    /// Finds an [OptionsValidator] method on a type (legacy support).
    /// </summary>
    /// <param name="typeSymbol">The type symbol to search.</param>
    /// <returns>Validator method info, or null if no validator method found.</returns>
    public static OptionsValidatorMethodInfo? GetOptionsValidatorMethod(INamedTypeSymbol typeSymbol)
    {
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            foreach (var attribute in method.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass == null)
                    continue;

                var name = attributeClass.Name;
                var fullName = attributeClass.ToDisplayString();

                if (name == OptionsValidatorAttributeName || fullName == OptionsValidatorAttributeFullName)
                {
                    return new OptionsValidatorMethodInfo(method.Name, method.IsStatic);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Information about an [OptionsValidator] method.
    /// </summary>
    public readonly struct OptionsValidatorMethodInfo
    {
        public OptionsValidatorMethodInfo(string methodName, bool isStatic)
        {
            MethodName = methodName;
            IsStatic = isStatic;
        }

        public string MethodName { get; }
        public bool IsStatic { get; }
    }

    #endregion
}
