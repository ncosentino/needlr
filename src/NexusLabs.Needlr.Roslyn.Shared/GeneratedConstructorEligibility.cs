using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NexusLabs.Needlr.Roslyn.Shared;

/// <summary>
/// Shared, symbol-level facts about Needlr's generated-constructor feature
/// (<c>[GenerateConstructor]</c> and field-level constructor guard attributes), used by
/// every Roslyn component that must agree on whether a type's effective constructor is
/// produced by the <c>GeneratedConstructorGenerator</c> pass rather than by a
/// hand-written constructor.
/// </summary>
/// <remarks>
/// This class intentionally exposes only stable symbol-level facts (attribute identity,
/// field eligibility, trigger detection, and overall eligibility) and not the main
/// generator's emission model -- guard resolution, parameter-name normalization, and
/// source text generation remain specific to <c>NexusLabs.Needlr.Generators</c>. A
/// consuming component that also needs the fully resolved model (parameter names,
/// resolved guard calls) should combine these facts with its own emission logic, the
/// way <c>ConstructorGenerationDiscoveryHelper</c> does.
/// </remarks>
internal static class GeneratedConstructorEligibility
{
    private const string NeedlrGeneratorsNamespace = "NexusLabs.Needlr.Generators";
    private const string GenerateConstructorAttributeName = "GenerateConstructorAttribute";
    private const string ConstructorGuardAttributeName = "ConstructorGuardAttribute";
    private const string ConstructorIgnoreAttributeName = "ConstructorIgnoreAttribute";
    private const string ConstructorGuardDefinitionAttributeName = "ConstructorGuardDefinitionAttribute";

    /// <summary>
    /// The exact suffix <c>GeneratedConstructorGenerator</c> uses for every hint name it
    /// passes to <c>AddSource</c> (see <c>GeneratedConstructorGenerator.BuildHintName</c>).
    /// Matching this precise suffix -- rather than the repository-wide <c>.g.cs</c>
    /// generated-file convention shared by every other source generator -- is required so
    /// a hand-authored constructor in some unrelated generated-looking file (e.g. a
    /// checked-in <c>Service.g.cs</c>) is never mistaken for this feature's own
    /// generated constructor.
    /// </summary>
    private const string GeneratedConstructorFileSuffix = ".GeneratedConstructor.g.cs";

    /// <summary>
    /// Matches a Needlr attribute class by BOTH simple name and containing namespace, per
    /// the discovery-helper convention. This deliberately avoids comparing a full
    /// display-string name, which would incorrectly match an unrelated third-party or
    /// application-defined attribute that merely shares the same simple name.
    /// </summary>
    internal static bool IsNeedlrGeneratorsAttribute(INamedTypeSymbol attributeClass, string simpleName)
    {
        return attributeClass.Name == simpleName &&
            attributeClass.ContainingNamespace?.ToDisplayString() == NeedlrGeneratorsNamespace;
    }

    /// <summary>
    /// True when the type carries a <c>[GenerateConstructor]</c> class-level attribute,
    /// regardless of its configured guard mode.
    /// </summary>
    internal static bool HasGenerateConstructorAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is { } attrClass && IsNeedlrGeneratorsAttribute(attrClass, GenerateConstructorAttributeName))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when the field carries <c>[ConstructorIgnore]</c>, excluding it from
    /// generated-constructor parameters even though it would otherwise be eligible.
    /// </summary>
    internal static bool HasConstructorIgnoreAttribute(IFieldSymbol field)
    {
        foreach (var attribute in field.GetAttributes())
        {
            if (attribute.AttributeClass is { } attrClass && IsNeedlrGeneratorsAttribute(attrClass, ConstructorIgnoreAttributeName))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when the field carries a guard attribute that positively triggers
    /// generated-constructor generation on its own (i.e. without a class-level
    /// <c>[GenerateConstructor]</c> attribute): a <c>[ConstructorGuard]</c> with a
    /// built-in kind other than <c>None</c>, a <c>[ConstructorGuard]</c> selecting a
    /// custom guard type, or any alias attribute meta-annotated with
    /// <c>[ConstructorGuardDefinition]</c>. Exclusion-only shapes (a bare
    /// <c>[ConstructorGuard(ConstructorGuardKind.None)]</c>, or no guard attribute at
    /// all) are not positive triggers.
    /// </summary>
    internal static bool HasPositiveConstructorGuardTrigger(IFieldSymbol field)
    {
        foreach (var attribute in field.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;

            if (IsNeedlrGeneratorsAttribute(attrClass, ConstructorGuardAttributeName))
            {
                if (IsPositiveConstructorGuardAttribute(attribute))
                    return true;

                continue;
            }

            if (HasConstructorGuardDefinitionMetaAttribute(attrClass))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when any eligible constructor field on the type has a positive guard
    /// trigger. See <see cref="HasPositiveConstructorGuardTrigger(IFieldSymbol)"/>.
    /// </summary>
    internal static bool HasPositiveFieldGuardTrigger(INamedTypeSymbol typeSymbol)
    {
        foreach (var field in GetEligibleConstructorFields(typeSymbol))
        {
            if (HasPositiveConstructorGuardTrigger(field))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the private, readonly, non-initialized instance fields of
    /// <paramref name="typeSymbol"/> that are eligible to become generated-constructor
    /// parameters (excluding any field carrying <c>[ConstructorIgnore]</c>), in
    /// deterministic declaration order. This is the exact field set and order that
    /// <c>GeneratedConstructorGenerator</c> emits as the constructor's parameter list,
    /// so callers that need to match the generated constructor's actual signature
    /// (parameter order, parameter types) must use this method rather than re-deriving
    /// their own field filter.
    /// </summary>
    internal static IReadOnlyList<IFieldSymbol> GetEligibleConstructorFields(INamedTypeSymbol typeSymbol)
    {
        var result = new List<IFieldSymbol>();

        foreach (var field in GetOrderedInstanceFields(typeSymbol))
        {
            if (!IsEligibleField(field) || HasConstructorIgnoreAttribute(field))
                continue;

            result.Add(field);
        }

        return result;
    }

    /// <summary>
    /// True when <paramref name="typeSymbol"/> is eligible for generated-constructor
    /// generation, i.e. <c>GeneratedConstructorGenerator</c> will emit a public
    /// constructor for it (or would, once the type also satisfies parameter-name
    /// uniqueness after normalization -- a rare edge case this method does not evaluate,
    /// since it requires the main generator's parameter-name normalization). Callers
    /// that only need to know whether a type's <em>effective</em> constructor requires
    /// arguments (as opposed to the implicit parameterless constructor still visible to
    /// a sibling generator/analyzer pass within the same compilation) can rely on this
    /// method instead of duplicating the same shape checks.
    /// </summary>
    internal static bool IsEligibleForGeneratedConstructor(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeKind != TypeKind.Class || typeSymbol.IsRecord)
            return false;

        // Nested types are unsupported: a nested type's enclosing instance (when the
        // outer type is non-static) is not something a generated constructor knows how
        // to obtain, so this feature is restricted to top-level types.
        if (typeSymbol.ContainingType != null)
            return false;

        // Base types requiring constructor arguments are unsupported. A base type
        // with an accessible parameterless constructor (including the common case of no
        // explicit base constructors at all, e.g. BackgroundService) is fully supported
        // since the generated constructor relies on the implicit `: base()` call.
        if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object &&
            !HasAccessibleParameterlessConstructor(typeSymbol.BaseType))
        {
            return false;
        }

        if (GetEligibleConstructorFields(typeSymbol).Count == 0)
            return false;

        if (!HasGenerateConstructorAttribute(typeSymbol) && !HasPositiveFieldGuardTrigger(typeSymbol))
            return false;

        if (!IsDeclaredPartial(typeSymbol))
            return false;

        if (HasExplicitInstanceConstructor(typeSymbol))
            return false;

        return true;
    }

    /// <summary>
    /// True when <paramref name="baseType"/> has an accessible parameterless
    /// constructor (public, protected, or protected-internal), including the implicit
    /// case where no constructor is declared at all.
    /// </summary>
    internal static bool HasAccessibleParameterlessConstructor(INamedTypeSymbol baseType)
    {
        if (baseType.InstanceConstructors.Length == 0)
            return true;

        foreach (var ctor in baseType.InstanceConstructors)
        {
            if (ctor.Parameters.Length != 0)
                continue;

            if (ctor.DeclaredAccessibility == Accessibility.Public ||
                ctor.DeclaredAccessibility == Accessibility.Protected ||
                ctor.DeclaredAccessibility == Accessibility.ProtectedOrInternal)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when any part of <paramref name="typeSymbol"/>'s declaration uses the
    /// <c>partial</c> modifier. A generated constructor can only be contributed to a
    /// partial type. Checked against <see cref="TypeDeclarationSyntax"/> generally
    /// (rather than only <see cref="ClassDeclarationSyntax"/>) so an analyzer can also
    /// evaluate this rule against a record declaration before reporting the separate
    /// unsupported-shape diagnostic for that record.
    /// </summary>
    internal static bool IsDeclaredPartial(INamedTypeSymbol typeSymbol)
    {
        foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is TypeDeclarationSyntax typeDeclaration &&
                typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a declaration is the canonical source declaration for its
    /// type, ordered by file path and source position.
    /// </summary>
    internal static bool IsCanonicalDeclaration(
        INamedTypeSymbol typeSymbol,
        TypeDeclarationSyntax typeDeclaration)
    {
        var canonical = typeSymbol.DeclaringSyntaxReferences
            .OrderBy(reference => reference.SyntaxTree.FilePath, System.StringComparer.Ordinal)
            .ThenBy(reference => reference.Span.Start)
            .FirstOrDefault();

        return canonical is not null &&
            canonical.SyntaxTree == typeDeclaration.SyntaxTree &&
            canonical.Span == typeDeclaration.Span;
    }

    /// <summary>
    /// True when <paramref name="typeSymbol"/> already declares an explicit,
    /// hand-written instance constructor. Generated-constructor generation is skipped
    /// entirely for such a type, to preserve one unambiguous constructor per the
    /// feature's contract.
    /// </summary>
    /// <remarks>
    /// A constructor declared in a file whose path ends with the exact
    /// <see cref="GeneratedConstructorFileSuffix"/> hint-name suffix
    /// <c>GeneratedConstructorGenerator</c> uses for its own output is never treated as
    /// a conflicting hand-written constructor. This method is used both by the source
    /// generator itself (which only ever observes a pre-generation compilation and so
    /// never sees its own output) and by diagnostic analyzers packaged alongside it
    /// (which see the fully generator-augmented compilation, where the type's own
    /// already-generated constructor would otherwise be indistinguishable from a real,
    /// hand-written one). Matching only this precise suffix -- rather than the
    /// repository-wide <c>.g.cs</c> generated-file convention shared by every source
    /// generator -- ensures a hand-authored constructor checked into some unrelated
    /// <c>*.g.cs</c>-suffixed file still correctly conflicts.
    /// </remarks>
    internal static bool HasExplicitInstanceConstructor(INamedTypeSymbol typeSymbol)
    {
        foreach (var ctor in typeSymbol.InstanceConstructors)
        {
            if (!ctor.IsImplicitlyDeclared && !IsDeclaredInGeneratedConstructorFile(ctor))
                return true;
        }

        return false;
    }

    private static bool IsDeclaredInGeneratedConstructorFile(ISymbol symbol)
    {
        foreach (var location in symbol.Locations)
        {
            var filePath = location.SourceTree?.FilePath;
            if (filePath != null && filePath.EndsWith(GeneratedConstructorFileSuffix, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsPositiveConstructorGuardAttribute(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
            return false;

        var first = attribute.ConstructorArguments[0];

        // Kind-based guard: None (0) is the only non-positive built-in kind.
        if (first.Kind == TypedConstantKind.Enum && first.Value is int enumValue)
            return enumValue != 0;

        // A resolved custom guard type reference is always positive.
        return first.Value is ITypeSymbol;
    }

    private static bool HasConstructorGuardDefinitionMetaAttribute(INamedTypeSymbol attributeClass)
    {
        foreach (var metaAttribute in attributeClass.GetAttributes())
        {
            if (metaAttribute.AttributeClass is { } metaClass && IsNeedlrGeneratorsAttribute(metaClass, ConstructorGuardDefinitionAttributeName))
                return true;
        }

        return false;
    }

    private static List<IFieldSymbol> GetOrderedInstanceFields(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsConst && !f.IsImplicitlyDeclared)
            .OrderBy(f => f.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? string.Empty, System.StringComparer.Ordinal)
            .ThenBy(f => f.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0)
            .ToList();
    }

    private static bool IsEligibleField(IFieldSymbol field)
    {
        return GetFieldIneligibilityReason(field) is null;
    }

    /// <summary>
    /// Returns a short, human-readable reason why <paramref name="field"/> cannot
    /// participate in generated-constructor generation, or <see langword="null"/> when
    /// the field's shape is eligible (a private, instance, readonly field without an
    /// initializer). Used by analyzers to report a targeted diagnostic when a
    /// constructor guard attribute is applied to a field that generation would
    /// otherwise silently skip.
    /// </summary>
    internal static string? GetFieldIneligibilityReason(IFieldSymbol field)
    {
        if (field.IsImplicitlyDeclared)
            return "compiler-generated";

        if (field.IsStatic)
            return "static";

        if (field.DeclaredAccessibility != Accessibility.Private)
            return "not private";

        if (!field.IsReadOnly)
            return "not readonly";

        if (HasInitializer(field))
            return "initialized with a field initializer";

        return null;
    }

    private static bool HasInitializer(IFieldSymbol field)
    {
        foreach (var syntaxRef in field.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is VariableDeclaratorSyntax { Initializer: not null })
                return true;
        }

        return false;
    }
}
