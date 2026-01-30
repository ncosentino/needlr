using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Helper for discovering factory generation attributes from Roslyn symbols.
/// </summary>
internal static class FactoryDiscoveryHelper
{
    private const string GenerateFactoryAttributeName = "GenerateFactoryAttribute";
    private const string GenerateFactoryAttributeFullName = "NexusLabs.Needlr.GenerateFactoryAttribute";

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

            var injectableParams = new List<TypeDiscoveryHelper.ConstructorParameterInfo>();
            var runtimeParams = new List<TypeDiscoveryHelper.ConstructorParameterInfo>();

            foreach (var param in ctor.Parameters)
            {
                var typeName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                paramDocs.TryGetValue(param.Name, out var docComment);

                if (IsInjectableParameterType(param.Type))
                {
                    // Check for [FromKeyedServices] attribute
                    var serviceKey = GetFromKeyedServicesKey(param);
                    var paramInfo = new TypeDiscoveryHelper.ConstructorParameterInfo(typeName, serviceKey, param.Name, docComment);
                    injectableParams.Add(paramInfo);
                }
                else
                {
                    var paramInfo = new TypeDiscoveryHelper.ConstructorParameterInfo(typeName, null, param.Name, docComment);
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

    private static bool IsInjectableParameterType(ITypeSymbol typeSymbol)
    {
        // Interfaces and abstract classes are typically injectable
        if (typeSymbol.TypeKind == TypeKind.Interface)
            return true;

        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsAbstract)
            return true;

        // Common framework types are injectable
        var fullName = typeSymbol.ToDisplayString();
        if (fullName.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal))
            return true;

        // Classes with [Singleton], [Scoped], or [Transient] are injectable
        if (typeSymbol is INamedTypeSymbol classType)
        {
            foreach (var attr in classType.GetAttributes())
            {
                var attrName = attr.AttributeClass?.Name;
                if (attrName is "SingletonAttribute" or "ScopedAttribute" or "TransientAttribute")
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Represents a constructor suitable for factory generation.
    /// </summary>
    public readonly struct FactoryConstructorInfo
    {
        public FactoryConstructorInfo(
            TypeDiscoveryHelper.ConstructorParameterInfo[] injectableParameters,
            TypeDiscoveryHelper.ConstructorParameterInfo[] runtimeParameters)
        {
            InjectableParameters = injectableParameters;
            RuntimeParameters = runtimeParameters;
        }

        /// <summary>Parameters that can be resolved from the service provider.</summary>
        public TypeDiscoveryHelper.ConstructorParameterInfo[] InjectableParameters { get; }

        /// <summary>Parameters that must be provided at factory call time.</summary>
        public TypeDiscoveryHelper.ConstructorParameterInfo[] RuntimeParameters { get; }
    }
}
