using System.Collections.Generic;

using Microsoft.CodeAnalysis;

using NexusLabs.Needlr.Generators.Models;
using NexusLabs.Needlr.Roslyn.Shared;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Discovers and normalizes direct constructor guards and custom guard aliases on a
/// field or property.
/// </summary>
internal static class ConstructorGuardDiscoveryHelper
{
    private const string ConstructorGuardAttributeName = "ConstructorGuardAttribute";
    private const string ConstructorGuardDefinitionAttributeName = "ConstructorGuardDefinitionAttribute";
    private const string DefaultGuardMethodName = "Validate";

    /// <summary>
    /// Returns every direct or aliased constructor guard on
    /// <paramref name="member"/>, preserving attribute declaration order.
    /// </summary>
    internal static ConstructorGuardModel[] GetExplicitGuards(ISymbol member)
    {
        var guards = new List<ConstructorGuardModel>();

        foreach (var attribute in member.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;

            if (IsConstructorGuardAttributeClass(attrClass))
            {
                var parsed = ParseConstructorGuardAttribute(attribute);
                if (parsed.HasValue)
                    guards.Add(parsed.Value);

                continue;
            }

            if (!TryGetGuardDefinition(attrClass, out var guardTypeName, out var guardMethodName))
                continue;

            var forwardedArgumentLiterals = TryRenderForwardedArguments(attribute);
            if (forwardedArgumentLiterals is null)
            {
                // Unsupported arguments invalidate the complete guard call so the
                // generator never emits a partial or positionally incorrect call.
                continue;
            }

            guards.Add(new ConstructorGuardModel(
                GeneratedConstructorGuardKind.Custom,
                guardTypeName,
                guardMethodName,
                forwardedArgumentLiterals));
        }

        return guards.ToArray();
    }

    /// <summary>
    /// Returns whether <paramref name="attributeClass"/> is Needlr's direct
    /// <c>ConstructorGuardAttribute</c>.
    /// </summary>
    internal static bool IsConstructorGuardAttributeClass(INamedTypeSymbol attributeClass)
    {
        return GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
            attributeClass,
            ConstructorGuardAttributeName);
    }

    /// <summary>
    /// Resolves a custom guard alias definition from an attribute type.
    /// </summary>
    internal static bool TryGetGuardDefinition(
        INamedTypeSymbol attributeClass,
        out string guardTypeName,
        out string guardMethodName)
    {
        foreach (var metaAttribute in attributeClass.GetAttributes())
        {
            var metaClass = metaAttribute.AttributeClass;
            if (metaClass is null ||
                !GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                    metaClass,
                    ConstructorGuardDefinitionAttributeName))
            {
                continue;
            }

            if (metaAttribute.ConstructorArguments.Length == 0 ||
                metaAttribute.ConstructorArguments[0].Value is not ITypeSymbol guardType)
            {
                continue;
            }

            guardTypeName = guardType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            guardMethodName = metaAttribute.ConstructorArguments.Length > 1 &&
                metaAttribute.ConstructorArguments[1].Value is string explicitName
                    ? explicitName
                    : DefaultGuardMethodName;
            return true;
        }

        guardTypeName = string.Empty;
        guardMethodName = string.Empty;
        return false;
    }

    private static string[]? TryRenderForwardedArguments(AttributeData attribute)
    {
        if (attribute.NamedArguments.Length > 0)
            return null;

        if (attribute.ConstructorArguments.Length == 0)
            return System.Array.Empty<string>();

        var rendered = new string[attribute.ConstructorArguments.Length];
        for (var i = 0; i < attribute.ConstructorArguments.Length; i++)
        {
            if (!TypedConstantRenderer.TryRender(attribute.ConstructorArguments[i], out var literal))
                return null;

            rendered[i] = literal;
        }

        return rendered;
    }

    private static ConstructorGuardModel? ParseConstructorGuardAttribute(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
            return null;

        var first = attribute.ConstructorArguments[0];

        if (first.Kind == TypedConstantKind.Enum && first.Value is int enumValue)
            return new ConstructorGuardModel(
                (GeneratedConstructorGuardKind)enumValue,
                null,
                null,
                null);

        if (first.Value is not ITypeSymbol guardType)
            return null;

        var methodName = attribute.ConstructorArguments.Length > 1 &&
            attribute.ConstructorArguments[1].Value is string explicitName
                ? explicitName
                : DefaultGuardMethodName;

        return new ConstructorGuardModel(
            GeneratedConstructorGuardKind.Custom,
            guardType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            methodName,
            null);
    }
}
