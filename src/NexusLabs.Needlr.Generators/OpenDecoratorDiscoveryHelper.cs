using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Helper for discovering open generic decorator types from Roslyn symbols.
/// </summary>
internal static class OpenDecoratorDiscoveryHelper
{
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
}
