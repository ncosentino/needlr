using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NexusLabs.Needlr.Generators.CodeGen;
using NexusLabs.Needlr.Generators.Models;
using NexusLabs.Needlr.Roslyn.Shared;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Discovers top-level partial positional record classes with marked property
/// parameters and builds equatable constructor-overload models.
/// </summary>
internal static class RecordConstructorOverloadDiscoveryHelper
{
    private const string GenerateConstructorAttributeName =
        "GenerateConstructorAttribute";
    private const string RecordConstructorOverloadParameterAttributeName =
        "RecordConstructorOverloadParameterAttribute";
    private const string GeneratedFileSuffix =
        ".RecordConstructorOverload.g.cs";

    /// <summary>
    /// Cheap syntax predicate for the per-record incremental pipeline.
    /// </summary>
    internal static bool IsCandidateRecordDeclaration(SyntaxNode node)
    {
        return node is RecordDeclarationSyntax;
    }

    /// <summary>
    /// Builds one canonical model for a record, or <see langword="null"/> when the
    /// record does not participate or any declaration is invalid.
    /// </summary>
    internal static RecordConstructorOverloadModel? TryCreateCanonicalModel(
        GeneratorSyntaxContext context)
    {
        var recordDeclaration = (RecordDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(recordDeclaration) is not
            INamedTypeSymbol typeSymbol ||
            !GeneratedConstructorEligibility.IsCanonicalDeclaration(
                typeSymbol,
                recordDeclaration))
        {
            return null;
        }

        return TryGetModel(
            typeSymbol,
            context.SemanticModel.Compilation);
    }

    /// <summary>
    /// Returns every directly declared property carrying the record-overload marker in
    /// deterministic source order.
    /// </summary>
    internal static IReadOnlyList<IPropertySymbol> GetMarkedProperties(
        INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(HasMarker)
            .OrderBy(
                property =>
                    property.Locations.FirstOrDefault()?.SourceTree?.FilePath ??
                    string.Empty,
                System.StringComparer.Ordinal)
            .ThenBy(
                property =>
                    property.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0)
            .ToArray();
    }

    /// <summary>
    /// Returns whether a property carries Needlr's record-overload marker.
    /// </summary>
    internal static bool HasMarker(IPropertySymbol property)
    {
        return GetMarkerAttribute(property) is not null;
    }

    /// <summary>
    /// Gets the marker attribute applied to a property.
    /// </summary>
    internal static AttributeData? GetMarkerAttribute(IPropertySymbol property)
    {
        foreach (var attribute in property.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeClass &&
                GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                    attributeClass,
                    RecordConstructorOverloadParameterAttributeName))
            {
                return attribute;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the positional record declaration containing the primary parameter list.
    /// </summary>
    internal static RecordDeclarationSyntax? GetPrimaryRecordDeclaration(
        INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<RecordDeclarationSyntax>()
            .FirstOrDefault(declaration => declaration.ParameterList is not null);
    }

    /// <summary>
    /// Returns why a marked property's containing type is outside the supported
    /// record-only contract.
    /// </summary>
    internal static string? GetTypeIneligibilityReason(
        INamedTypeSymbol typeSymbol)
    {
        if (!typeSymbol.IsRecord)
        {
            return typeSymbol.TypeKind == TypeKind.Class
                ? "an ordinary class rather than a positional record class"
                : $"a {typeSymbol.TypeKind.ToString().ToLowerInvariant()} rather than a positional record class";
        }

        if (typeSymbol.TypeKind == TypeKind.Struct)
            return "a record struct";

        if (typeSymbol.IsFileLocal)
            return "a file-local record that cannot be extended from a generated file";

        if (typeSymbol.ContainingType is not null)
            return "a nested record";

        if (GetPrimaryRecordDeclaration(typeSymbol) is null)
            return "a non-positional record with no primary parameter list";

        if (typeSymbol.BaseType is not null &&
            typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
        {
            return "an inherited record";
        }

        return null;
    }

    /// <summary>
    /// Returns why a marked property cannot participate, or
    /// <see langword="null"/> when it is assignable by the generated constructor.
    /// </summary>
    internal static string? GetPropertyIneligibilityReason(
        INamedTypeSymbol containingType,
        IPropertySymbol property,
        RecordDeclarationSyntax primaryDeclaration)
    {
        if (!SymbolEqualityComparer.Default.Equals(
            property.ContainingType,
            containingType))
        {
            return "inherited rather than declared directly by the record";
        }

        if (property.IsStatic)
            return "static";

        if (property.IsIndexer)
            return "an indexer";

        if (IsPositionalProperty(property, primaryDeclaration))
        {
            return "a positional property synthesized from a primary constructor parameter";
        }

        if (property.SetMethod is null)
        {
            return "get-only and cannot be assigned by the generated constructor";
        }

        if (property.ExplicitInterfaceImplementations.Length > 0)
        {
            return "an explicit interface implementation and cannot be assigned by name";
        }

        if (property.IsAbstract)
        {
            return "abstract and has no assignable implementation on the record";
        }

        if (property.IsRequired)
        {
            return "required; the generated overload does not claim to satisfy the record's complete required-member contract";
        }

        if (!IsTypeAccessibleFromGeneratedConstructor(
            property.Type,
            containingType.DeclaredAccessibility))
        {
            return $"typed as '{property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}', which is less accessible than the generated public constructor";
        }

        return null;
    }

    /// <summary>
    /// Returns whether the record also participates in field-based
    /// <c>GenerateConstructor</c> generation.
    /// </summary>
    internal static bool HasFieldBasedGeneratedConstructorTrigger(
        INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeClass &&
                GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                    attributeClass,
                    GenerateConstructorAttributeName))
            {
                return true;
            }
        }

        return GeneratedConstructorEligibility.HasPositiveFieldGuardTrigger(
            typeSymbol);
    }

    /// <summary>
    /// Finds an existing constructor whose C# signature collides with the proposed
    /// overload, ignoring names, nullable annotations, optional values, and
    /// <c>params</c>.
    /// </summary>
    internal static bool TryGetSignatureCollision(
        INamedTypeSymbol typeSymbol,
        Compilation compilation,
        out string collisionDisplay)
    {
        var primaryDeclaration = GetPrimaryRecordDeclaration(typeSymbol);
        if (primaryDeclaration?.ParameterList is null)
        {
            collisionDisplay = string.Empty;
            return false;
        }

        var semanticModel = compilation.GetSemanticModel(
            primaryDeclaration.SyntaxTree);
        var proposed = new List<(ITypeSymbol Type, RefKind RefKind)>();
        foreach (var parameter in primaryDeclaration.ParameterList.Parameters)
        {
            if (semanticModel.GetDeclaredSymbol(parameter) is not
                IParameterSymbol parameterSymbol)
            {
                collisionDisplay = string.Empty;
                return false;
            }

            proposed.Add((parameterSymbol.Type, parameterSymbol.RefKind));
        }

        foreach (var property in GetMarkedProperties(typeSymbol))
        {
            proposed.Add((property.Type, RefKind.None));
        }

        foreach (var constructor in typeSymbol.InstanceConstructors)
        {
            if (IsDeclaredInGeneratedFile(constructor) ||
                constructor.Parameters.Length != proposed.Count)
            {
                continue;
            }

            var matches = true;
            for (var i = 0; i < proposed.Count; i++)
            {
                if (constructor.Parameters[i].RefKind != proposed[i].RefKind ||
                    !AreSignatureTypesEquivalent(
                        constructor.Parameters[i].Type,
                        proposed[i].Type))
                {
                    matches = false;
                    break;
                }
            }

            if (!matches)
                continue;

            collisionDisplay = BuildConstructorDisplay(constructor);
            return true;
        }

        collisionDisplay = string.Empty;
        return false;
    }

    private static RecordConstructorOverloadModel? TryGetModel(
        INamedTypeSymbol typeSymbol,
        Compilation compilation)
    {
        var markedProperties = GetMarkedProperties(typeSymbol);
        if (markedProperties.Count == 0 ||
            GetTypeIneligibilityReason(typeSymbol) is not null ||
            !GeneratedConstructorEligibility.IsDeclaredPartial(typeSymbol) ||
            HasFieldBasedGeneratedConstructorTrigger(typeSymbol))
        {
            return null;
        }

        var primaryDeclaration = GetPrimaryRecordDeclaration(typeSymbol);
        if (primaryDeclaration?.ParameterList is null)
            return null;

        var semanticModel = compilation.GetSemanticModel(
            primaryDeclaration.SyntaxTree);
        var primaryParameters =
            new RecordConstructorPrimaryParameter[
                primaryDeclaration.ParameterList.Parameters.Count];
        var primaryParameterNames = new HashSet<string>(
            System.StringComparer.Ordinal);

        for (var i = 0;
            i < primaryDeclaration.ParameterList.Parameters.Count;
            i++)
        {
            var parameterSyntax =
                primaryDeclaration.ParameterList.Parameters[i];
            if (semanticModel.GetDeclaredSymbol(parameterSyntax) is not
                IParameterSymbol parameterSymbol ||
                !primaryParameterNames.Add(parameterSymbol.Name))
            {
                return null;
            }

            var documentation =
                DocumentationCommentHelper.GetParameterDocumentation(
                    typeSymbol,
                    parameterSymbol.Name) ??
                $"The value forwarded to the positional primary constructor parameter <paramref name=\"{parameterSymbol.Name}\"/>.";
            primaryParameters[i] = new RecordConstructorPrimaryParameter(
                parameterSymbol.Name,
                GeneratorHelpers.EscapeIdentifier(parameterSymbol.Name),
                parameterSymbol.Type.ToDisplayString(
                    ConstructorGenerationDiscoveryHelper.NullableAwareFormat),
                GetDeclarationModifier(parameterSymbol.RefKind),
                GetArgumentModifier(parameterSymbol.RefKind),
                documentation);
        }

        var propertyParameters =
            new RecordConstructorPropertyParameter[markedProperties.Count];
        for (var i = 0; i < markedProperties.Count; i++)
        {
            var property = markedProperties[i];
            if (GetPropertyIneligibilityReason(
                typeSymbol,
                property,
                primaryDeclaration) is not null ||
                !primaryParameterNames.Add(property.Name))
            {
                return null;
            }

            var effectiveGuards =
                ConstructorGuardCodeGenerator.ComposeEffectiveGuards(
                    false,
                    ConstructorGuardDiscoveryHelper.GetExplicitGuards(property));
            if (!ArePropertyGuardsValid(
                compilation,
                typeSymbol,
                property))
            {
                return null;
            }

            var parameterType = property.Type;
            if (parameterType.IsReferenceType &&
                parameterType.NullableAnnotation ==
                    NullableAnnotation.Annotated &&
                ConstructorGuardCodeGenerator.HasBuiltInNullRejectingGuard(
                    effectiveGuards))
            {
                parameterType = parameterType.WithNullableAnnotation(
                    NullableAnnotation.NotAnnotated);
            }

            var escapedName = GeneratorHelpers.EscapeIdentifier(property.Name);
            var documentation =
                DocumentationCommentHelper.GetSummaryDocumentation(property) ??
                $"The value assigned to <see cref=\"{escapedName}\"/>.";
            propertyParameters[i] =
                new RecordConstructorPropertyParameter(
                    property.Name,
                    escapedName,
                    parameterType.ToDisplayString(
                        ConstructorGenerationDiscoveryHelper.NullableAwareFormat),
                    documentation,
                    effectiveGuards);
        }

        if (TryGetSignatureCollision(
            typeSymbol,
            compilation,
            out _))
        {
            return null;
        }

        var typeParameterList = typeSymbol.TypeParameters.Length == 0
            ? string.Empty
            : "<" + string.Join(
                ", ",
                typeSymbol.TypeParameters.Select(
                    parameter =>
                        GeneratorHelpers.EscapeIdentifier(parameter.Name))) +
                ">";
        var containingNamespace =
            typeSymbol.ContainingNamespace is { IsGlobalNamespace: false }
                ? typeSymbol.ContainingNamespace.ToDisplayString()
                : string.Empty;

        return new RecordConstructorOverloadModel(
            containingNamespace,
            typeSymbol.Name,
            GeneratorHelpers.EscapeIdentifier(typeSymbol.Name),
            typeParameterList,
            typeSymbol.TypeParameters.Length,
            primaryParameters,
            propertyParameters,
            primaryDeclaration.SyntaxTree.FilePath);
    }

    private static bool ArePropertyGuardsValid(
        Compilation compilation,
        INamedTypeSymbol containingType,
        IPropertySymbol property)
    {
        foreach (var attribute in property.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass)
                continue;

            ConstructorGuardOccurrence occurrence;
            if (ConstructorGuardDiscoveryHelper.IsConstructorGuardAttributeClass(
                attributeClass))
            {
                occurrence =
                    ConstructorGuardAnalysisHelper.BuildDirectGuardOccurrence(
                        property,
                        property.Type,
                        "property",
                        attribute,
                        null);
            }
            else if (ConstructorGuardAnalysisHelper.TryGetGuardDefinition(
                attributeClass,
                out var guardType,
                out var methodName,
                out var methodNameExplicit))
            {
                occurrence =
                    ConstructorGuardAnalysisHelper.BuildAliasOccurrence(
                        property,
                        property.Type,
                        "property",
                        attribute,
                        null,
                        guardType,
                        methodName,
                        methodNameExplicit,
                        attributeClass.DeclaringSyntaxReferences.Length > 0);
            }
            else
            {
                continue;
            }

            if (!ConstructorGuardAnalysisHelper
                .IsPositiveGuardOccurrenceValidForGeneration(
                    compilation,
                    containingType,
                    occurrence))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPositionalProperty(
        IPropertySymbol property,
        RecordDeclarationSyntax primaryDeclaration)
    {
        return primaryDeclaration.ParameterList?.Parameters.Any(
            parameter => parameter.Identifier.ValueText == property.Name) == true;
    }

    private static string GetDeclarationModifier(RefKind refKind)
    {
        return refKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            RefKind.RefReadOnlyParameter => "ref readonly ",
            _ => string.Empty,
        };
    }

    private static string GetArgumentModifier(RefKind refKind)
    {
        return refKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In or RefKind.RefReadOnlyParameter => "in ",
            _ => string.Empty,
        };
    }

    private static bool IsDeclaredInGeneratedFile(ISymbol symbol)
    {
        return symbol.Locations.Any(location =>
            location.SourceTree?.FilePath.EndsWith(
                GeneratedFileSuffix,
                System.StringComparison.Ordinal) == true);
    }

    private static string BuildConstructorDisplay(IMethodSymbol constructor)
    {
        return $"{constructor.ContainingType.Name}({string.Join(", ", constructor.Parameters.Select(parameter => parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))})";
    }

    private static bool AreSignatureTypesEquivalent(
        ITypeSymbol left,
        ITypeSymbol right)
    {
        if (SymbolEqualityComparer.Default.Equals(left, right))
            return true;

        if ((left is IDynamicTypeSymbol &&
                right.SpecialType == SpecialType.System_Object) ||
            (right is IDynamicTypeSymbol &&
                left.SpecialType == SpecialType.System_Object))
        {
            return true;
        }

        if (left is IArrayTypeSymbol leftArray &&
            right is IArrayTypeSymbol rightArray)
        {
            return leftArray.Rank == rightArray.Rank &&
                AreSignatureTypesEquivalent(
                    leftArray.ElementType,
                    rightArray.ElementType);
        }

        if (left is IPointerTypeSymbol leftPointer &&
            right is IPointerTypeSymbol rightPointer)
        {
            return AreSignatureTypesEquivalent(
                leftPointer.PointedAtType,
                rightPointer.PointedAtType);
        }

        if (left is not INamedTypeSymbol leftNamed ||
            right is not INamedTypeSymbol rightNamed ||
            !SymbolEqualityComparer.Default.Equals(
                leftNamed.OriginalDefinition,
                rightNamed.OriginalDefinition) ||
            leftNamed.TypeArguments.Length != rightNamed.TypeArguments.Length)
        {
            return false;
        }

        for (var i = 0; i < leftNamed.TypeArguments.Length; i++)
        {
            if (!AreSignatureTypesEquivalent(
                leftNamed.TypeArguments[i],
                rightNamed.TypeArguments[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTypeAccessibleFromGeneratedConstructor(
        ITypeSymbol type,
        Accessibility containingTypeAccessibility)
    {
        switch (type)
        {
            case IArrayTypeSymbol arrayType:
                return IsTypeAccessibleFromGeneratedConstructor(
                    arrayType.ElementType,
                    containingTypeAccessibility);
            case IPointerTypeSymbol pointerType:
                return IsTypeAccessibleFromGeneratedConstructor(
                    pointerType.PointedAtType,
                    containingTypeAccessibility);
            case ITypeParameterSymbol:
            case IDynamicTypeSymbol:
                return true;
            case INamedTypeSymbol namedType:
                if (!IsNamedTypeAccessibleFromGeneratedConstructor(
                    namedType,
                    containingTypeAccessibility))
                {
                    return false;
                }

                return namedType.TypeArguments.All(typeArgument =>
                    IsTypeAccessibleFromGeneratedConstructor(
                        typeArgument,
                        containingTypeAccessibility));
            default:
                return true;
        }
    }

    private static bool IsNamedTypeAccessibleFromGeneratedConstructor(
        INamedTypeSymbol type,
        Accessibility containingTypeAccessibility)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.SpecialType != SpecialType.None)
                continue;

            if (containingTypeAccessibility == Accessibility.Public)
            {
                if (current.DeclaredAccessibility != Accessibility.Public)
                    return false;
            }
            else if (current.DeclaredAccessibility is not (
                Accessibility.Public or
                Accessibility.Internal or
                Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }
}
