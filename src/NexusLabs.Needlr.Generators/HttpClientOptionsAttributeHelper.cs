using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Helper for discovering <c>[HttpClientOptions]</c> attributes and the associated
/// capability interfaces on Roslyn symbols. Also resolves the HttpClient name from the
/// three supported sources (attribute argument, <c>ClientName</c> property, type-name
/// inference).
/// </summary>
internal static class HttpClientOptionsAttributeHelper
{
    private const string HttpClientOptionsAttributeName = "HttpClientOptionsAttribute";
    private const string GeneratorsNamespace = "NexusLabs.Needlr.Generators";

    private const string INamedHttpClientOptionsName = "INamedHttpClientOptions";
    private const string IHttpClientTimeoutName = "IHttpClientTimeout";
    private const string IHttpClientUserAgentName = "IHttpClientUserAgent";
    private const string IHttpClientBaseAddressName = "IHttpClientBaseAddress";
    private const string IHttpClientDefaultHeadersName = "IHttpClientDefaultHeaders";

    /// <summary>Suffixes stripped from the type name (in order) when inferring the client name.</summary>
    private static readonly string[] ClientNameSuffixes = ["HttpClientOptions", "HttpClientSettings", "HttpClient"];

    /// <summary>
    /// Extracted state from an <c>[HttpClientOptions]</c> attribute on a type.
    /// </summary>
    public readonly struct HttpClientOptionsAttributeInfo
    {
        public HttpClientOptionsAttributeInfo(string? sectionName, string? name, Location? attributeLocation)
        {
            SectionName = sectionName;
            Name = name;
            AttributeLocation = attributeLocation;
        }

        /// <summary>Explicit section name from the attribute, or null to infer.</summary>
        public string? SectionName { get; }

        /// <summary>Explicit client name override from the attribute, or null to fall through.</summary>
        public string? Name { get; }

        /// <summary>Source location of the attribute, used for analyzer diagnostics.</summary>
        public Location? AttributeLocation { get; }
    }

    /// <summary>
    /// Checks if a type has the <c>[HttpClientOptions]</c> attribute.
    /// </summary>
    public static bool HasHttpClientOptionsAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (IsHttpClientOptionsAttribute(attribute.AttributeClass))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts the <c>[HttpClientOptions]</c> attribute info from a type, or <c>null</c>
    /// if the attribute is not present.
    /// </summary>
    public static HttpClientOptionsAttributeInfo? GetHttpClientOptionsAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!IsHttpClientOptionsAttribute(attribute.AttributeClass))
                continue;

            string? sectionName = null;
            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string section)
            {
                sectionName = section;
            }

            string? name = null;
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == "Name" && namedArg.Value.Value is string n)
                {
                    name = n;
                }
            }

            var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();
            return new HttpClientOptionsAttributeInfo(sectionName, name, location);
        }

        return null;
    }

    /// <summary>
    /// Detects which v1 capability interfaces the type implements. Returns a bit flag set
    /// which drives the conditional emission in <c>HttpClientCodeGenerator</c>.
    /// </summary>
    public static HttpClientCapabilities DetectCapabilities(INamedTypeSymbol typeSymbol)
    {
        var caps = HttpClientCapabilities.None;

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.ContainingNamespace?.ToDisplayString() != GeneratorsNamespace)
                continue;

            switch (iface.Name)
            {
                case IHttpClientTimeoutName:
                    caps |= HttpClientCapabilities.Timeout;
                    break;
                case IHttpClientUserAgentName:
                    caps |= HttpClientCapabilities.UserAgent;
                    break;
                case IHttpClientBaseAddressName:
                    caps |= HttpClientCapabilities.BaseAddress;
                    break;
                case IHttpClientDefaultHeadersName:
                    caps |= HttpClientCapabilities.Headers;
                    break;
            }
        }

        return caps;
    }

    /// <summary>
    /// Returns <c>true</c> if the type implements <c>INamedHttpClientOptions</c>.
    /// </summary>
    public static bool ImplementsNamedHttpClientOptions(INamedTypeSymbol typeSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.Name == INamedHttpClientOptionsName &&
                iface.ContainingNamespace?.ToDisplayString() == GeneratorsNamespace)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves the HttpClient name from the three allowed sources, in precedence order:
    /// (1) attribute <c>Name</c>, (2) <c>ClientName</c> property literal body,
    /// (3) inferred from type name with suffix stripping.
    /// </summary>
    /// <param name="typeSymbol">The options type.</param>
    /// <param name="attributeInfo">The extracted attribute info for the type.</param>
    /// <param name="propertyNameFromType">
    /// The literal <c>ClientName</c> property value if present and resolvable, or <c>null</c>.
    /// </param>
    /// <param name="resolvedName">The resolved client name on success.</param>
    /// <returns><c>true</c> if a non-empty name could be resolved; otherwise <c>false</c>.</returns>
    public static bool TryResolveClientName(
        INamedTypeSymbol typeSymbol,
        HttpClientOptionsAttributeInfo attributeInfo,
        string? propertyNameFromType,
        out string resolvedName)
    {
        if (!string.IsNullOrWhiteSpace(attributeInfo.Name))
        {
            resolvedName = attributeInfo.Name!;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(propertyNameFromType))
        {
            resolvedName = propertyNameFromType!;
            return true;
        }

        var inferred = InferClientNameFromTypeName(typeSymbol.Name);
        if (!string.IsNullOrWhiteSpace(inferred))
        {
            resolvedName = inferred;
            return true;
        }

        resolvedName = string.Empty;
        return false;
    }

    /// <summary>
    /// Strips known suffixes from a type name to infer a client name.
    /// Returns the original name if no suffix matches.
    /// </summary>
    public static string InferClientNameFromTypeName(string typeName)
    {
        foreach (var suffix in ClientNameSuffixes)
        {
            if (typeName.EndsWith(suffix, System.StringComparison.Ordinal) &&
                typeName.Length > suffix.Length)
            {
                return typeName.Substring(0, typeName.Length - suffix.Length);
            }
        }

        return typeName;
    }

    /// <summary>
    /// Attempts to read a <c>ClientName</c> property from the type and extract its literal
    /// expression value. Returns a tri-state:
    /// <list type="bullet">
    /// <item><description><c>Absent</c> — no <c>ClientName</c> property declared</description></item>
    /// <item><description><c>Literal</c> — <c>ClientName</c> exists with a string literal expression body; value is in <paramref name="literalValue"/></description></item>
    /// <item><description><c>NonLiteral</c> — <c>ClientName</c> exists but its body is not a simple literal (e.g., computed, field-backed, method call) — the analyzer reports NDLRHTTP003 for this case</description></item>
    /// </list>
    /// </summary>
    public static ClientNamePropertyResult TryGetClientNameProperty(
        INamedTypeSymbol typeSymbol,
        out string? literalValue)
    {
        literalValue = null;
        IPropertySymbol? clientNameProp = null;

        foreach (var member in typeSymbol.GetMembers("ClientName"))
        {
            if (member is IPropertySymbol p)
            {
                clientNameProp = p;
                break;
            }
        }

        if (clientNameProp is null)
            return ClientNamePropertyResult.Absent;

        // Must be a string-typed, readable, instance property — otherwise treat as non-literal
        // and let the analyzer handle it (NDLRHTTP006 fires on shape violations).
        if (clientNameProp.Type.SpecialType != SpecialType.System_String || clientNameProp.IsStatic)
            return ClientNamePropertyResult.NonLiteral;

        foreach (var declRef in clientNameProp.DeclaringSyntaxReferences)
        {
            var syntax = declRef.GetSyntax();
            if (syntax is not PropertyDeclarationSyntax propSyntax)
                continue;

            // Expression-bodied arrow form: public string ClientName => "WebFetch";
            if (propSyntax.ExpressionBody is ArrowExpressionClauseSyntax arrow &&
                arrow.Expression is LiteralExpressionSyntax exprLit &&
                exprLit.IsKind(SyntaxKind.StringLiteralExpression))
            {
                literalValue = exprLit.Token.ValueText;
                return ClientNamePropertyResult.Literal;
            }

            // Getter-only body form: public string ClientName { get { return "WebFetch"; } }
            if (propSyntax.AccessorList is { } accessors)
            {
                foreach (var accessor in accessors.Accessors)
                {
                    if (!accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
                        continue;

                    // Expression body on getter
                    if (accessor.ExpressionBody is ArrowExpressionClauseSyntax getArrow &&
                        getArrow.Expression is LiteralExpressionSyntax getExprLit &&
                        getExprLit.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        literalValue = getExprLit.Token.ValueText;
                        return ClientNamePropertyResult.Literal;
                    }

                    // Single-return block body
                    if (accessor.Body is { } block &&
                        block.Statements.Count == 1 &&
                        block.Statements[0] is ReturnStatementSyntax ret &&
                        ret.Expression is LiteralExpressionSyntax retLit &&
                        retLit.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        literalValue = retLit.Token.ValueText;
                        return ClientNamePropertyResult.Literal;
                    }
                }
            }
        }

        return ClientNamePropertyResult.NonLiteral;
    }

    /// <summary>
    /// Computes the configuration section name from the attribute (if explicit) or by inference
    /// from the resolved client name.
    /// </summary>
    public static string ResolveSectionName(HttpClientOptionsAttributeInfo attributeInfo, string clientName)
    {
        if (!string.IsNullOrWhiteSpace(attributeInfo.SectionName))
            return attributeInfo.SectionName!;

        return $"HttpClients:{clientName}";
    }

    /// <summary>
    /// Returns the symbol for the <c>ClientName</c> property if present, for analyzer shape checks.
    /// </summary>
    public static IPropertySymbol? GetClientNamePropertySymbol(INamedTypeSymbol typeSymbol)
    {
        foreach (var member in typeSymbol.GetMembers("ClientName"))
        {
            if (member is IPropertySymbol p)
                return p;
        }
        return null;
    }

    private static bool IsHttpClientOptionsAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
            return false;

        return attributeClass.Name == HttpClientOptionsAttributeName &&
               attributeClass.ContainingNamespace?.ToDisplayString() == GeneratorsNamespace;
    }
}

/// <summary>
/// Tri-state result from probing a type for a <c>ClientName</c> property.
/// </summary>
internal enum ClientNamePropertyResult
{
    Absent,
    Literal,
    NonLiteral,
}
