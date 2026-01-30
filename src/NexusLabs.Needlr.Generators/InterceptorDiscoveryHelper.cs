using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Helper for discovering interceptor attributes from Roslyn symbols.
/// </summary>
internal static class InterceptorDiscoveryHelper
{
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
            var interceptorTypeName = TypeDiscoveryHelper.GetFullyQualifiedName(interceptorType);

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
                var interceptorTypeName = TypeDiscoveryHelper.GetFullyQualifiedName(interceptorType);

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
                    TypeDiscoveryHelper.GetFullyQualifiedName(iface),
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

    private static bool IsSystemInterface(INamedTypeSymbol interfaceSymbol)
    {
        var ns = interfaceSymbol.ContainingNamespace?.ToDisplayString();
        return ns is not null && (ns.StartsWith("System", StringComparison.Ordinal) || 
                                  ns.StartsWith("Microsoft", StringComparison.Ordinal));
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
}
