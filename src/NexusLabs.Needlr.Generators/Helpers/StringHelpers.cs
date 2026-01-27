using System.Text.RegularExpressions;

namespace NexusLabs.Needlr.Generators.Helpers;

/// <summary>
/// Helper methods for string manipulation and escaping.
/// </summary>
internal static class StringHelpers
{
    /// <summary>
    /// Escapes a string for use in a regular C# string literal (quoted string).
    /// </summary>
    public static string EscapeStringLiteral(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Escapes a string for use in a C# verbatim string literal (@"...").
    /// </summary>
    public static string EscapeVerbatimStringLiteral(string value)
    {
        // In verbatim strings, only quotes need escaping (doubled)
        return value.Replace("\"", "\"\"");
    }

    /// <summary>
    /// Escapes content for use in XML documentation comments.
    /// </summary>
    public static string EscapeXmlContent(string content)
    {
        return content
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    /// <summary>
    /// Gets the simple type name without namespace or generic markers.
    /// </summary>
    public static string GetSimpleTypeName(string fullyQualifiedName)
    {
        var name = fullyQualifiedName.Replace("global::", "");
        var lastDot = name.LastIndexOf('.');
        var simpleName = lastDot >= 0 ? name.Substring(lastDot + 1) : name;
        
        // Remove generic suffix if present
        var genericIndex = simpleName.IndexOf('<');
        return genericIndex >= 0 ? simpleName.Substring(0, genericIndex) : simpleName;
    }

    /// <summary>
    /// Converts a PascalCase name to camelCase.
    /// </summary>
    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>
    /// Removes the global:: prefix from a type name if present.
    /// </summary>
    public static string StripGlobalPrefix(string name)
    {
        return name.StartsWith("global::") ? name.Substring(8) : name;
    }

    /// <summary>
    /// Extracts the generic type argument from a generic type name.
    /// E.g., "IHandler&lt;Order&gt;" returns "Order".
    /// </summary>
    public static string ExtractGenericTypeArgument(string genericTypeName)
    {
        var start = genericTypeName.IndexOf('<');
        var end = genericTypeName.LastIndexOf('>');
        if (start < 0 || end < 0 || end <= start)
            return genericTypeName;
        return genericTypeName.Substring(start + 1, end - start - 1);
    }

    /// <summary>
    /// Gets the base name of a generic type (before the generic marker).
    /// E.g., "LoggingDecorator`1" returns "LoggingDecorator".
    /// </summary>
    public static string GetGenericBaseName(string typeName)
    {
        var backtickIndex = typeName.IndexOf('`');
        if (backtickIndex > 0)
            return typeName.Substring(0, backtickIndex);
        
        var angleBracketIndex = typeName.IndexOf('<');
        if (angleBracketIndex > 0)
            return typeName.Substring(0, angleBracketIndex);
        
        return typeName;
    }

    /// <summary>
    /// Gets the proxy type name for an intercepted service.
    /// E.g., "global::MyApp.MyService" returns "MyService_InterceptorProxy".
    /// </summary>
    public static string GetProxyTypeName(string fullyQualifiedTypeName)
    {
        return GetSimpleTypeName(fullyQualifiedTypeName) + "_InterceptorProxy";
    }
}
