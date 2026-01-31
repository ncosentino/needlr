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

            // Skip IHostedService - hosted services are registered separately via RegisterHostedServices()
            // to ensure proper concrete + interface forwarding pattern
            if (IsHostedServiceInterface(iface))
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

    private static bool IsHostedServiceInterface(INamedTypeSymbol interfaceSymbol)
    {
        var fullName = GetFullyQualifiedName(interfaceSymbol);
        return fullName == "global::Microsoft.Extensions.Hosting.IHostedService";
    }

    /// <summary>
    /// Determines whether a type is a hosted service (implements IHostedService or inherits from BackgroundService).
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <param name="isCurrentAssembly">True if the type is from the current compilation's assembly.</param>
    /// <returns>True if the type is a hosted service.</returns>
    public static bool IsHostedServiceType(INamedTypeSymbol typeSymbol, bool isCurrentAssembly = false)
    {
        // Must be a concrete, non-abstract class
        if (typeSymbol.IsAbstract || typeSymbol.TypeKind != TypeKind.Class)
            return false;

        // Check accessibility
        if (!isCurrentAssembly && IsInternalOrLessAccessible(typeSymbol))
            return false;

        // Skip if marked with [DoNotAutoRegister]
        if (HasDoNotAutoRegisterAttributeDirect(typeSymbol))
            return false;

        // Skip compiler-generated types
        if (IsCompilerGenerated(typeSymbol))
            return false;

        // Skip decorators - types with [DecoratorFor<IHostedService>] should not be
        // registered as hosted services (they decorate hosted services, not are hosted services)
        if (IsDecoratorForHostedService(typeSymbol))
            return false;

        // Check if inherits from BackgroundService
        if (InheritsFromBackgroundService(typeSymbol))
            return true;

        // Check if directly implements IHostedService (not via BackgroundService)
        if (ImplementsIHostedService(typeSymbol))
            return true;

        return false;
    }

    private static bool IsDecoratorForHostedService(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass == null)
                continue;

            var attrFullName = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            
            // Check for DecoratorForAttribute<IHostedService>
            if (attrFullName.StartsWith("global::NexusLabs.Needlr.DecoratorForAttribute<", StringComparison.Ordinal))
            {
                // Get the type argument
                if (attrClass.IsGenericType && attrClass.TypeArguments.Length == 1)
                {
                    var typeArg = attrClass.TypeArguments[0];
                    if (typeArg is INamedTypeSymbol namedTypeArg)
                    {
                        var typeArgName = GetFullyQualifiedName(namedTypeArg);
                        if (typeArgName == "global::Microsoft.Extensions.Hosting.IHostedService")
                            return true;
                    }
                }
            }
        }
        return false;
    }

    private static bool InheritsFromBackgroundService(INamedTypeSymbol typeSymbol)
    {
        var baseType = typeSymbol.BaseType;
        while (baseType != null)
        {
            var fullName = GetFullyQualifiedName(baseType);
            if (fullName == "global::Microsoft.Extensions.Hosting.BackgroundService")
                return true;
            baseType = baseType.BaseType;
        }
        return false;
    }

    private static bool ImplementsIHostedService(INamedTypeSymbol typeSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            var fullName = GetFullyQualifiedName(iface);
            if (fullName == "global::Microsoft.Extensions.Hosting.IHostedService")
                return true;
        }
        return false;
    }

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
    /// Checks if a type implements IDisposable or IAsyncDisposable.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type implements IDisposable or IAsyncDisposable.</returns>
    public static bool IsDisposableType(INamedTypeSymbol typeSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            var fullName = GetFullyQualifiedName(iface);
            if (fullName == "global::System.IDisposable" || fullName == "global::System.IAsyncDisposable")
                return true;
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

        // Skip types with [DoNotAutoRegister] directly on the type itself
        // NOTE: We use the "Direct" version because [DoNotAutoRegister] on interfaces
        // (like IServiceCollectionPlugin) means "don't inject as DI service", not
        // "don't discover plugins implementing this interface"
        if (HasDoNotAutoRegisterAttributeDirect(typeSymbol))
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

    // NOTE: HasKernelFunctions was moved to NexusLabs.Needlr.SemanticKernel.Generators
    // NOTE: HubRegistrationInfo was moved to NexusLabs.Needlr.SignalR.Generators

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

    // NOTE: TryGetHubRegistrationInfo, TryGetPropertyStringValue, TryGetPropertyTypeValue 
    // were moved to NexusLabs.Needlr.SignalR.Generators

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
}