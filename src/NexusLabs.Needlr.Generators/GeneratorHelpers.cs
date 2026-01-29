// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Utility methods for source code generation.
/// </summary>
internal static class GeneratorHelpers
{
    /// <summary>
    /// Sanitizes an assembly name to be a valid C# identifier for use in namespaces.
    /// </summary>
    public static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Generated";

        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
            }
            else if (c == '.' || c == '-' || c == ' ')
            {
                // Keep dots for namespace segments, replace dashes/spaces with underscores
                sb.Append(c == '.' ? '.' : '_');
            }
            // Skip other characters
        }

        var result = sb.ToString();

        // Ensure each segment doesn't start with a digit
        var segments = result.Split('.');
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length > 0 && char.IsDigit(segments[i][0]))
            {
                segments[i] = "_" + segments[i];
            }
        }

        return string.Join(".", segments.Where(s => s.Length > 0));
    }

    /// <summary>
    /// Escapes a string for use in a regular C# string literal.
    /// </summary>
    public static string EscapeStringLiteral(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Escapes a string for use in a verbatim C# string literal.
    /// </summary>
    public static string EscapeVerbatimStringLiteral(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        // In verbatim strings, only double-quotes need escaping (by doubling them)
        return value.Replace("\"", "\"\"");
    }

    /// <summary>
    /// Escapes content for use in XML documentation.
    /// </summary>
    public static string EscapeXmlContent(string content)
    {
        // The content from GetDocumentationCommentXml() is already parsed,
        // so entities like &lt; are already decoded. We need to re-encode them.
        return content
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    /// <summary>
    /// Gets the simple type name from a fully qualified name.
    /// "global::System.String" -> "String"
    /// </summary>
    public static string GetSimpleTypeName(string fullyQualifiedName)
    {
        var parts = fullyQualifiedName.Split('.');
        return parts[parts.Length - 1];
    }

    /// <summary>
    /// Converts a name to camelCase, removing leading 'I' for interfaces.
    /// </summary>
    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        
        // Remove leading 'I' for interfaces
        if (name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
            name = name.Substring(1);
        
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>
    /// Strips the "global::" prefix from a type name if present.
    /// </summary>
    public static string StripGlobalPrefix(string name)
    {
        return name.StartsWith("global::", StringComparison.Ordinal) 
            ? name.Substring(8) 
            : name;
    }

    /// <summary>
    /// Gets the short type name from a fully qualified name.
    /// Removes global:: prefix and namespace.
    /// </summary>
    public static string GetShortTypeName(string fullyQualifiedTypeName)
    {
        var name = fullyQualifiedTypeName;
        if (name.StartsWith("global::", StringComparison.Ordinal))
            name = name.Substring(8);
        
        var lastDot = name.LastIndexOf('.');
        return lastDot >= 0 ? name.Substring(lastDot + 1) : name;
    }

    /// <summary>
    /// Gets the proxy type name for an intercepted service.
    /// </summary>
    public static string GetProxyTypeName(string fullyQualifiedTypeName)
    {
        var shortName = GetShortTypeName(fullyQualifiedTypeName);
        return $"{shortName}_InterceptorProxy";
    }

    /// <summary>
    /// Gets the fully qualified validator class name for an options type.
    /// E.g., "global::TestApp.StripeOptions" -> "global::TestApp.Generated.StripeOptionsValidator"
    /// </summary>
    public static string GetValidatorClassName(string optionsTypeName)
    {
        var shortName = GetShortTypeName(optionsTypeName);
        
        var name = optionsTypeName;
        if (name.StartsWith("global::", StringComparison.Ordinal))
            name = name.Substring(8);
        
        var lastDot = name.LastIndexOf('.');
        var ns = lastDot >= 0 ? name.Substring(0, lastDot) : "";
        
        var validatorName = shortName + "Validator";
        return string.IsNullOrEmpty(ns)
            ? $"global::{validatorName}"
            : $"global::{ns}.Generated.{validatorName}";
    }

    /// <summary>
    /// Extracts the generic type argument from a generic type name.
    /// E.g., "Task&lt;string&gt;" -> "string"
    /// </summary>
    public static string ExtractGenericTypeArgument(string genericTypeName)
    {
        var openBracket = genericTypeName.IndexOf('<');
        var closeBracket = genericTypeName.LastIndexOf('>');
        if (openBracket >= 0 && closeBracket > openBracket)
        {
            return genericTypeName.Substring(openBracket + 1, closeBracket - openBracket - 1);
        }
        return "object";
    }

    /// <summary>
    /// Gets the base name of a generic type (without type arguments).
    /// E.g., "IHandler&lt;Order&gt;" -> "IHandler"
    /// </summary>
    public static string GetGenericBaseName(string typeName)
    {
        var angleBracketIndex = typeName.IndexOf('<');
        return angleBracketIndex >= 0 ? typeName.Substring(0, angleBracketIndex) : typeName;
    }

    /// <summary>
    /// Creates a closed generic type name from an open generic decorator and a closed interface.
    /// For example: LoggingDecorator{T} + IHandler{Order} = LoggingDecorator{Order}
    /// </summary>
    public static string CreateClosedGenericType(string openDecoratorTypeName, string closedInterfaceName, string openInterfaceName)
    {
        var closedArgs = ExtractGenericArguments(closedInterfaceName);
        var openDecoratorBaseName = GetGenericBaseName(openDecoratorTypeName);
        
        if (closedArgs.Length == 0)
            return openDecoratorTypeName;
        
        return $"{openDecoratorBaseName}<{string.Join(", ", closedArgs)}>";
    }

    /// <summary>
    /// Extracts the generic type arguments from a closed generic type name.
    /// For example: "IHandler{Order, Payment}" returns ["Order", "Payment"]
    /// </summary>
    public static string[] ExtractGenericArguments(string typeName)
    {
        var angleBracketIndex = typeName.IndexOf('<');
        if (angleBracketIndex < 0)
            return Array.Empty<string>();

        var argsStart = angleBracketIndex + 1;
        var argsEnd = typeName.LastIndexOf('>');
        if (argsEnd <= argsStart)
            return Array.Empty<string>();

        var argsString = typeName.Substring(argsStart, argsEnd - argsStart);
        
        // Handle nested generics by parsing with bracket depth tracking
        var args = new List<string>();
        var depth = 0;
        var start = 0;
        
        for (int i = 0; i < argsString.Length; i++)
        {
            var c = argsString[i];
            if (c == '<') depth++;
            else if (c == '>') depth--;
            else if (c == ',' && depth == 0)
            {
                args.Add(argsString.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        
        // Add the last argument
        if (start < argsString.Length)
            args.Add(argsString.Substring(start).Trim());
        
        return args.ToArray();
    }

    /// <summary>
    /// Converts a type name to a valid Mermaid node ID.
    /// </summary>
    public static string GetMermaidNodeId(string typeName)
    {
        return GetShortTypeName(typeName).Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "_");
    }

    /// <summary>
    /// Calculates a percentage, handling division by zero.
    /// </summary>
    public static int Percentage(int count, int total)
    {
        if (total == 0) return 0;
        return (int)Math.Round(100.0 * count / total);
    }
}
