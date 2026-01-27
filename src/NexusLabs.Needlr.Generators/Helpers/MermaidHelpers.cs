using System.Text;
using System.Text.RegularExpressions;

namespace NexusLabs.Needlr.Generators.Helpers;

/// <summary>
/// Helper methods for generating Mermaid diagram content.
/// </summary>
internal static class MermaidHelpers
{
    /// <summary>
    /// Creates a valid Mermaid node ID from a fully qualified type name.
    /// </summary>
    public static string GetMermaidNodeId(string typeName)
    {
        // Remove global:: prefix and sanitize for Mermaid
        var name = typeName.Replace("global::", "")
                          .Replace(".", "_")
                          .Replace("<", "_")
                          .Replace(">", "_")
                          .Replace(",", "_")
                          .Replace(" ", "");
        return name;
    }

    /// <summary>
    /// Gets the short type name without namespace (e.g., "MyService" from "global::MyApp.MyService").
    /// </summary>
    public static string GetShortTypeName(string fullyQualifiedTypeName)
    {
        // Remove global:: prefix
        var name = fullyQualifiedTypeName.Replace("global::", "");
        
        // Find the last dot that's not inside generic brackets
        var bracketDepth = 0;
        var lastDot = -1;
        for (var i = name.Length - 1; i >= 0; i--)
        {
            if (name[i] == '>') bracketDepth++;
            else if (name[i] == '<') bracketDepth--;
            else if (name[i] == '.' && bracketDepth == 0)
            {
                lastDot = i;
                break;
            }
        }
        
        return lastDot >= 0 ? name.Substring(lastDot + 1) : name;
    }

    /// <summary>
    /// Sanitizes a string for use as a Mermaid subgraph identifier.
    /// </summary>
    public static string SanitizeIdentifier(string name)
    {
        return Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
    }

    /// <summary>
    /// Gets the Mermaid shape notation for a type based on its characteristics.
    /// </summary>
    /// <param name="isDecorator">True if this is a decorator type (uses stadium shape).</param>
    /// <param name="hasFactory">True if this has [GenerateFactory] (uses hexagon shape).</param>
    /// <param name="isInterceptor">True if this is an interceptor (uses subroutine shape).</param>
    /// <returns>Tuple of (startShape, endShape) for Mermaid notation.</returns>
    public static (string Start, string End) GetNodeShape(bool isDecorator, bool hasFactory, bool isInterceptor)
    {
        if (isDecorator) return ("[[", "]]");      // Stadium shape for decorators
        if (hasFactory) return ("{{", "}}");       // Hexagon shape for factory sources
        if (isInterceptor) return ("[[", "]]");    // Stadium shape for interceptors too
        return ("[", "]");                          // Rectangle for normal types
    }

    /// <summary>
    /// Gets the Mermaid edge notation for different relationship types.
    /// </summary>
    public static string GetEdgeNotation(EdgeType edgeType)
    {
        return edgeType switch
        {
            EdgeType.Dependency => "-->",           // Solid arrow: A depends on B
            EdgeType.Interface => "-.->",           // Dotted arrow: Interface to implementation
            EdgeType.Produces => "-.->|produces|",  // Labeled dotted: Factory produces type
            EdgeType.Decorates => "-->",            // Solid arrow: Decorator wraps
            EdgeType.Intercepts => "-.->|intercepts|", // Labeled dotted: Interceptor intercepts
            _ => "-->"
        };
    }
}

/// <summary>
/// Types of edges in Mermaid dependency graphs.
/// </summary>
internal enum EdgeType
{
    Dependency,
    Interface,
    Produces,
    Decorates,
    Intercepts
}
