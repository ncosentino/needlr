using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Helper for discovering plugin order attributes from Roslyn symbols.
/// </summary>
internal static class PluginOrderHelper
{
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
}
