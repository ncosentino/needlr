using System;
using System.Globalization;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Renders a Roslyn <see cref="TypedConstant"/> -- an attribute constructor argument
/// value already validated by the compiler -- into the exact C# literal or expression
/// source text needed to forward it as a positional argument on a generated guard
/// method call.
/// </summary>
/// <remarks>
/// <para>
/// Only scalar constant shapes that can be rendered unambiguously and safely are
/// supported: <see langword="null"/>, <see langword="bool"/>, every signed and
/// unsigned integral primitive (with the C# suffix required for the literal to
/// round-trip to the same runtime type, e.g. <c>u</c>/<c>L</c>/<c>UL</c>),
/// <see langword="char"/>, <see langword="string"/>, enum members (rendered as a
/// fully qualified member reference, or a fully qualified cast expression when no
/// declared member matches the value exactly -- e.g. a combined <c>[Flags]</c> value),
/// and <see cref="System.Type"/> (rendered as <c>typeof(global::...)</c>, including
/// open generic type definitions such as <c>typeof(List&lt;&gt;)</c>).
/// </para>
/// <para>
/// Arrays/params and floating-point (<see langword="float"/>/<see langword="double"/>)
/// constants are deliberately unsupported in this slice: rendering an array positional
/// argument as a single forwarded literal is inherently ambiguous, and floating-point
/// literals can silently lose round-trip precision. A dedicated analyzer is
/// responsible for diagnosing an alias attribute usage whose positional arguments
/// include one of these unsupported shapes; this renderer only ever reports the
/// unsupported result explicitly via its <see langword="bool"/> return value -- it
/// never invents a success-shaped fallback string for a shape it cannot safely render.
/// </para>
/// <para>
/// This type never accepts or echoes raw source text: every rendered literal is
/// derived solely from the compiler-validated <see cref="TypedConstant"/> value, never
/// from unparsed attribute-argument syntax.
/// </para>
/// </remarks>
internal static class TypedConstantRenderer
{
    /// <summary>
    /// Attempts to render <paramref name="constant"/> as a C# literal or expression
    /// suitable for splicing directly into generated source. Returns
    /// <see langword="false"/> -- with <paramref name="renderedLiteral"/> set to
    /// <see cref="string.Empty"/> -- for any constant shape this renderer does not
    /// support (arrays/params, floating-point primitives, or any other unrecognized
    /// shape), rather than falling back to a best-effort rendering that could produce
    /// unsafe or ambiguous source.
    /// </summary>
    internal static bool TryRender(TypedConstant constant, out string renderedLiteral)
    {
        if (constant.IsNull)
        {
            renderedLiteral = "null";
            return true;
        }

        switch (constant.Kind)
        {
            case TypedConstantKind.Primitive:
                return TryRenderPrimitive(constant.Value, out renderedLiteral);

            case TypedConstantKind.Enum:
                return TryRenderEnum(constant, out renderedLiteral);

            case TypedConstantKind.Type:
                return TryRenderType(constant, out renderedLiteral);

            default:
                // TypedConstantKind.Array and TypedConstantKind.Error (and any future
                // kind this renderer doesn't yet know about) are unsupported. Note
                // that TypedConstant.Value throws for an array constant, so Kind must
                // be checked -- and rejected -- before any Value access is attempted.
                renderedLiteral = string.Empty;
                return false;
        }
    }

    private static bool TryRenderPrimitive(object? value, out string renderedLiteral)
    {
        switch (value)
        {
            case bool boolValue:
                renderedLiteral = boolValue ? "true" : "false";
                return true;

            case char charValue:
                renderedLiteral = SymbolDisplay.FormatLiteral(charValue, quote: true);
                return true;

            case string stringValue:
                renderedLiteral = SymbolDisplay.FormatLiteral(stringValue, quote: true);
                return true;

            case sbyte sbyteValue:
                renderedLiteral = sbyteValue.ToString(CultureInfo.InvariantCulture);
                return true;

            case byte byteValue:
                renderedLiteral = byteValue.ToString(CultureInfo.InvariantCulture);
                return true;

            case short shortValue:
                renderedLiteral = shortValue.ToString(CultureInfo.InvariantCulture);
                return true;

            case ushort ushortValue:
                renderedLiteral = ushortValue.ToString(CultureInfo.InvariantCulture);
                return true;

            case int intValue:
                renderedLiteral = intValue.ToString(CultureInfo.InvariantCulture);
                return true;

            case uint uintValue:
                renderedLiteral = uintValue.ToString(CultureInfo.InvariantCulture) + "u";
                return true;

            case long longValue:
                renderedLiteral = longValue.ToString(CultureInfo.InvariantCulture) + "L";
                return true;

            case ulong ulongValue:
                renderedLiteral = ulongValue.ToString(CultureInfo.InvariantCulture) + "UL";
                return true;

            default:
                // float, double (explicitly excluded from this slice), and anything
                // else unrecognized (e.g. decimal, which is not a legal attribute
                // constructor parameter type in the first place).
                renderedLiteral = string.Empty;
                return false;
        }
    }

    private static bool TryRenderEnum(TypedConstant constant, out string renderedLiteral)
    {
        if (constant.Type is not INamedTypeSymbol enumType)
        {
            renderedLiteral = string.Empty;
            return false;
        }

        var qualifiedEnumTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var matchingMember = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(member => member.IsConst && Equals(member.ConstantValue, constant.Value));

        if (matchingMember is not null)
        {
            renderedLiteral = $"{qualifiedEnumTypeName}.{matchingMember.Name}";
            return true;
        }

        // No single declared member matches the value exactly (for example, a
        // combined [Flags] value with no dedicated named member). A fully qualified
        // cast of the exact underlying value is still a safe, unambiguous rendering.
        var underlyingValueLiteral = Convert.ToString(constant.Value, CultureInfo.InvariantCulture);
        renderedLiteral = $"({qualifiedEnumTypeName}){underlyingValueLiteral}";
        return true;
    }

    private static bool TryRenderType(TypedConstant constant, out string renderedLiteral)
    {
        if (constant.Value is not ITypeSymbol typeSymbol)
        {
            renderedLiteral = string.Empty;
            return false;
        }

        renderedLiteral = $"typeof({typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
        return true;
    }
}
