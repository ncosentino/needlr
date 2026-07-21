using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NexusLabs.Needlr.Generators.Models;
using NexusLabs.Needlr.Roslyn.Shared;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Discovers whether a type is eligible for generated-constructor generation
/// (<c>[GenerateConstructor]</c> or a positive field-level constructor guard trigger)
/// and builds the shared <see cref="GeneratedConstructorModel"/> that both source
/// emission and Needlr's type-registry constructor discovery consume.
/// </summary>
/// <remarks>
/// Symbol-level eligibility facts (attribute identity, eligible field set, positive
/// field-guard trigger, overall eligibility) are delegated to
/// <see cref="GeneratedConstructorEligibility"/> so that this generator, the SignalR
/// generator, and Needlr's analyzers agree on the same rules. This class owns only the
/// main-generator-specific emission model: guard resolution, parameter-name
/// normalization, and the <see cref="GeneratedConstructorModel"/> shape.
/// </remarks>
internal static class ConstructorGenerationDiscoveryHelper
{
    private const string ConstructorGuardAttributeName = "ConstructorGuardAttribute";
    private const string ConstructorGuardDefinitionAttributeName = "ConstructorGuardDefinitionAttribute";
    private const string GenerateConstructorAttributeName = "GenerateConstructorAttribute";
    private const string DefaultGuardMethodName = "Validate";

    private static readonly SymbolDisplayFormat NullableAwareFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
        SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Builds the effective constructor-parameter list for a type that is eligible
    /// for generated-constructor generation, for use by Needlr's type-registry
    /// discovery. Returns <see langword="null"/> when the type is not eligible, in
    /// which case callers should fall back to symbol-based constructor discovery
    /// (which is correct both for types with a hand-written constructor and for
    /// referenced-assembly types whose generated constructor is already compiled).
    /// </summary>
    internal static IReadOnlyList<TypeDiscoveryHelper.ConstructorParameterInfo>? TryGetEffectiveConstructorParameters(INamedTypeSymbol typeSymbol)
    {
        var model = TryGetModel(typeSymbol);
        if (model is null)
            return null;

        // Only usable for Needlr's automatic DI discovery when every parameter is a
        // container-resolvable service type -- the same rule applied to hand-written
        // constructors via TypeDiscoveryHelper.IsInjectableParameterType. A
        // field-derived constructor with a string/value-type parameter (e.g. a
        // field-triggered guard on a plain string) is still a perfectly valid
        // generated constructor; it just isn't eligible for automatic registration
        // without a factory or explicit registration.
        foreach (var field in GeneratedConstructorEligibility.GetEligibleConstructorFields(typeSymbol))
        {
            if (!TypeDiscoveryHelper.IsInjectableParameterType(field.Type))
                return null;
        }

        var fields = model.Value.Fields;
        var result = new TypeDiscoveryHelper.ConstructorParameterInfo[fields.Length];
        for (var i = 0; i < fields.Length; i++)
        {
            result[i] = new TypeDiscoveryHelper.ConstructorParameterInfo(
                fields[i].ParameterTypeName,
                serviceKey: null,
                parameterName: fields[i].ParameterName);
        }

        return result;
    }

    /// <summary>
    /// Builds the full generated-constructor model for a type, or <see langword="null"/>
    /// when the type is not eligible for constructor generation.
    /// </summary>
    internal static GeneratedConstructorModel? TryGetModel(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeKind != TypeKind.Class || typeSymbol.IsRecord)
            return null;

        // Nested types are unsupported: a nested type's enclosing instance (when the
        // outer type is non-static) is not something a generated constructor knows how
        // to obtain, so this feature is restricted to top-level types.
        if (typeSymbol.ContainingType != null)
            return null;

        // Base types requiring constructor arguments are unsupported. A base type
        // with an accessible parameterless constructor (including the common case of no
        // explicit base constructors at all, e.g. BackgroundService) is fully supported
        // since the generated constructor relies on the implicit `: base()` call.
        if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object &&
            !GeneratedConstructorEligibility.HasAccessibleParameterlessConstructor(typeSymbol.BaseType))
        {
            return null;
        }

        var candidates = new List<(IFieldSymbol Field, ConstructorFieldGuard[] Guards)>();
        foreach (var field in GeneratedConstructorEligibility.GetEligibleConstructorFields(typeSymbol))
        {
            candidates.Add((field, GetExplicitGuards(field)));
        }

        var hasPositiveFieldTrigger = GeneratedConstructorEligibility.HasPositiveFieldGuardTrigger(typeSymbol);
        var classNullGuardMode = GetGenerateConstructorAttributeMode(typeSymbol);

        if (classNullGuardMode is null && !hasPositiveFieldTrigger)
            return null;

        if (!GeneratedConstructorEligibility.IsDeclaredPartial(typeSymbol))
            return null;

        if (GeneratedConstructorEligibility.HasExplicitInstanceConstructor(typeSymbol))
            return null;

        if (candidates.Count == 0)
            return null;

        var parameterNames = new HashSet<string>(System.StringComparer.Ordinal);
        var fields = new EligibleConstructorField[candidates.Count];
        for (var i = 0; i < candidates.Count; i++)
        {
            var (field, guards) = candidates[i];
            var parameterName = GetParameterName(field.Name);

            // Parameter-name collisions after normalization are unsupported; skip
            // generation entirely rather than emit a broken constructor.
            if (!parameterNames.Add(parameterName))
                return null;

            var parameterTypeName = field.Type.ToDisplayString(NullableAwareFormat);
            var isNonNullableReferenceType = field.Type.IsReferenceType && field.Type.NullableAnnotation == NullableAnnotation.NotAnnotated;

            fields[i] = new EligibleConstructorField(field.Name, parameterName, parameterTypeName, isNonNullableReferenceType, guards);
        }

        var typeParameterList = typeSymbol.TypeParameters.Length == 0
            ? string.Empty
            : "<" + string.Join(", ", typeSymbol.TypeParameters.Select(tp => tp.Name)) + ">";

        var containingNamespace = typeSymbol.ContainingNamespace is { IsGlobalNamespace: false }
            ? typeSymbol.ContainingNamespace.ToDisplayString()
            : string.Empty;

        var sourceFilePath = typeSymbol.Locations.FirstOrDefault(l => l.IsInSource)?.SourceTree?.FilePath;

        return new GeneratedConstructorModel(
            containingNamespace,
            typeSymbol.Name,
            typeParameterList,
            typeSymbol.TypeParameters.Length,
            classNullGuardMode ?? GeneratedConstructorNullGuardMode.None,
            fields,
            sourceFilePath);
    }

    /// <summary>
    /// The syntax-level predicate for the generator's per-type incremental pipeline:
    /// a cheap, semantic-model-free check for a top-level class declaration with at
    /// least one field member. Any class without a field can never be eligible (see
    /// <see cref="TryGetModel"/>), so this filters the overwhelming majority of
    /// classes in a typical compilation before a semantic model is ever touched.
    /// </summary>
    internal static bool IsCandidateClassDeclaration(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDeclaration && HasFieldMember(classDeclaration);
    }

    /// <summary>
    /// The transform for the generator's per-type incremental pipeline. Builds the
    /// full <see cref="GeneratedConstructorModel"/> for the syntax node's type,
    /// immediately converting the resolved <see cref="INamedTypeSymbol"/> into an
    /// equatable value model rather than passing the symbol itself further down the
    /// pipeline (symbols compare by reference across compilations and would defeat
    /// incremental caching).
    /// </summary>
    /// <remarks>
    /// For a partial type declared across multiple field-bearing declarations, this
    /// produces a model for exactly one of them -- the declaration earliest in
    /// deterministic (file path, then position) order -- and <see langword="null"/> for
    /// every other declaration of the same type, so the generator emits exactly one
    /// source file per type regardless of how many partial declarations carry fields.
    /// </remarks>
    internal static GeneratedConstructorModel? TryCreateCanonicalModel(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol typeSymbol)
            return null;

        if (!IsCanonicalFieldBearingDeclaration(typeSymbol, classDeclaration))
            return null;

        return TryGetModel(typeSymbol);
    }

    private static bool HasFieldMember(ClassDeclarationSyntax classDeclaration)
    {
        foreach (var member in classDeclaration.Members)
        {
            if (member is FieldDeclarationSyntax)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when <paramref name="classDeclaration"/> is, among every field-bearing
    /// partial declaration of <paramref name="typeSymbol"/>, the one earliest in
    /// deterministic (file path, then span start) order. Comparing file path and span
    /// rather than syntax-node identity keeps this correct even when a declaration is
    /// re-parsed into a new (but structurally equivalent) node instance.
    /// </summary>
    private static bool IsCanonicalFieldBearingDeclaration(INamedTypeSymbol typeSymbol, ClassDeclarationSyntax classDeclaration)
    {
        string? canonicalFilePath = null;
        var canonicalStart = 0;

        foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is not ClassDeclarationSyntax candidate || !HasFieldMember(candidate))
                continue;

            var filePath = candidate.SyntaxTree.FilePath;
            var start = candidate.SpanStart;

            if (canonicalFilePath is null ||
                ComparePosition(filePath, start, canonicalFilePath, canonicalStart) < 0)
            {
                canonicalFilePath = filePath;
                canonicalStart = start;
            }
        }

        if (canonicalFilePath is null)
            return false;

        return canonicalFilePath == classDeclaration.SyntaxTree.FilePath && canonicalStart == classDeclaration.SpanStart;
    }

    private static int ComparePosition(string filePathA, int startA, string filePathB, int startB)
    {
        var pathCompare = string.CompareOrdinal(filePathA, filePathB);
        return pathCompare != 0 ? pathCompare : startA.CompareTo(startB);
    }

    private static GeneratedConstructorNullGuardMode? GetGenerateConstructorAttributeMode(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;

            if (!GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(attrClass, GenerateConstructorAttributeName))
                continue;

            if (attribute.ConstructorArguments.Length == 0)
                return GeneratedConstructorNullGuardMode.None;

            var arg = attribute.ConstructorArguments[0];
            if (arg.Value is int modeValue)
                return (GeneratedConstructorNullGuardMode)modeValue;

            return GeneratedConstructorNullGuardMode.None;
        }

        return null;
    }

    private static ConstructorFieldGuard[] GetExplicitGuards(IFieldSymbol field)
    {
        var guards = new List<ConstructorFieldGuard>();

        foreach (var attribute in field.GetAttributes())
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

            if (TryGetGuardDefinition(attrClass, out var guardTypeName, out var guardMethodName))
            {
                guards.Add(new ConstructorFieldGuard(GeneratedConstructorGuardKind.Custom, guardTypeName, guardMethodName));
            }
        }

        return guards.ToArray();
    }

    private static bool IsConstructorGuardAttributeClass(INamedTypeSymbol attrClass)
    {
        return GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(attrClass, ConstructorGuardAttributeName);
    }

    private static ConstructorFieldGuard? ParseConstructorGuardAttribute(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
            return null;

        var first = attribute.ConstructorArguments[0];

        if (first.Kind == TypedConstantKind.Enum && first.Value is int enumValue)
        {
            return new ConstructorFieldGuard((GeneratedConstructorGuardKind)enumValue);
        }

        if (first.Value is ITypeSymbol guardType)
        {
            var methodName = attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[1].Value is string explicitName
                ? explicitName
                : DefaultGuardMethodName;

            return new ConstructorFieldGuard(GeneratedConstructorGuardKind.Custom, guardType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), methodName);
        }

        return null;
    }

    private static bool TryGetGuardDefinition(INamedTypeSymbol attrClass, out string guardTypeName, out string guardMethodName)
    {
        foreach (var metaAttribute in attrClass.GetAttributes())
        {
            var metaClass = metaAttribute.AttributeClass;
            if (metaClass is null)
                continue;

            if (!GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(metaClass, ConstructorGuardDefinitionAttributeName))
                continue;

            if (metaAttribute.ConstructorArguments.Length == 0 || metaAttribute.ConstructorArguments[0].Value is not ITypeSymbol guardType)
                continue;

            guardTypeName = guardType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            guardMethodName = metaAttribute.ConstructorArguments.Length > 1 && metaAttribute.ConstructorArguments[1].Value is string explicitName
                ? explicitName
                : DefaultGuardMethodName;
            return true;
        }

        guardTypeName = string.Empty;
        guardMethodName = string.Empty;
        return false;
    }

    /// <summary>
    /// Normalizes a field name into a constructor parameter name (e.g. <c>_repository</c>
    /// -> <c>repository</c>), escaping C# keywords with <c>@</c>. Internal so
    /// <see cref="FactoryDiscoveryHelper"/> can derive the exact same parameter names
    /// for a generated constructor's fields when building factory call sites.
    /// </summary>
    internal static string GetParameterName(string fieldName)
    {
        var name = fieldName;

        if (name.Length > 1 && name[0] == '_')
        {
            name = name.Substring(1);
        }

        if (name.Length == 0)
        {
            name = "value";
        }
        else if (char.IsUpper(name[0]))
        {
            name = char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        if (SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
        {
            name = "@" + name;
        }

        return name;
    }
}
