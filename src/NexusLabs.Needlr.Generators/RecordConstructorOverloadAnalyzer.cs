using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using NexusLabs.Needlr.Generators.Models;
using NexusLabs.Needlr.Roslyn.Shared;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Validates record constructor-overload markers, participating properties,
/// constructor guards, and proposed constructor signatures.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RecordConstructorOverloadAnalyzer : DiagnosticAnalyzer
{
    private const string ConstructorGuardAttributeName =
        "ConstructorGuardAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.RecordConstructorOverloadRequiresPartialType,
            DiagnosticDescriptors.RecordConstructorOverloadUnsupportedTypeShape,
            DiagnosticDescriptors.RecordConstructorOverloadPropertyUnsupported,
            DiagnosticDescriptors.ConstructorGuardOnNonparticipatingProperty,
            DiagnosticDescriptors.RecordConstructorOverloadConflictsWithGeneratedConstructor,
            DiagnosticDescriptors.RecordConstructorOverloadSignatureCollision,
            DiagnosticDescriptors.InvalidConstructorGuardEnumValue,
            DiagnosticDescriptors.ConstructorGuardIncompatibleWithFieldType,
            DiagnosticDescriptors.ConstructorGuardTypeInvalid,
            DiagnosticDescriptors.ConstructorGuardMethodNameInvalid,
            DiagnosticDescriptors.ConstructorGuardMethodInvalid,
            DiagnosticDescriptors.ConstructorGuardMethodAmbiguous,
            DiagnosticDescriptors.ConstructorGuardAliasUsageArgumentUnsupported,
            DiagnosticDescriptors.ConstructorGuardForwardedArgumentIncompatible);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeTypeDeclaration,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration);
    }

    private static void AnalyzeTypeDeclaration(
        SyntaxNodeAnalysisContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(typeDeclaration) is not
            INamedTypeSymbol typeSymbol ||
            !GeneratedConstructorEligibility.IsCanonicalDeclaration(
                typeSymbol,
                typeDeclaration))
        {
            return;
        }

        var properties = typeSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .ToArray();
        ReportNonparticipatingPropertyGuards(context, typeSymbol, properties);

        var markedProperties = properties
            .Where(
                RecordConstructorOverloadDiscoveryHelper.HasMarker)
            .OrderBy(
                property =>
                    property.Locations.FirstOrDefault()?.SourceTree?.FilePath ??
                    string.Empty,
                System.StringComparer.Ordinal)
            .ThenBy(
                property =>
                    property.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0)
            .ToArray();
        if (markedProperties.Length == 0)
            return;

        var typeLocation = typeDeclaration.Identifier.GetLocation();
        if (RecordConstructorOverloadDiscoveryHelper
            .HasFieldBasedGeneratedConstructorTrigger(typeSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.RecordConstructorOverloadConflictsWithGeneratedConstructor,
                typeLocation,
                typeSymbol.Name));
            return;
        }

        var typeReason =
            RecordConstructorOverloadDiscoveryHelper
                .GetTypeIneligibilityReason(typeSymbol);
        if (typeReason is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.RecordConstructorOverloadUnsupportedTypeShape,
                typeLocation,
                typeSymbol.Name,
                typeReason));
            return;
        }

        if (!GeneratedConstructorEligibility.IsDeclaredPartial(typeSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.RecordConstructorOverloadRequiresPartialType,
                typeLocation,
                typeSymbol.Name));
            return;
        }

        var primaryDeclaration =
            RecordConstructorOverloadDiscoveryHelper
                .GetPrimaryRecordDeclaration(typeSymbol);
        if (primaryDeclaration is null)
            return;

        var hasInvalidProperty = false;
        foreach (var property in markedProperties)
        {
            var propertyReason =
                RecordConstructorOverloadDiscoveryHelper
                    .GetPropertyIneligibilityReason(
                        typeSymbol,
                        property,
                        primaryDeclaration);
            if (propertyReason is not null)
            {
                var marker =
                    RecordConstructorOverloadDiscoveryHelper
                        .GetMarkerAttribute(property);
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.RecordConstructorOverloadPropertyUnsupported,
                    marker is null
                        ? property.Locations.FirstOrDefault() ?? Location.None
                        : ConstructorGuardAnalysisHelper.GetAttributeLocation(
                            context,
                            marker),
                    property.Name,
                    propertyReason));
                hasInvalidProperty = true;
                continue;
            }

            AnalyzeParticipatingPropertyGuards(
                context,
                typeSymbol,
                property);
        }

        if (hasInvalidProperty)
            return;

        if (RecordConstructorOverloadDiscoveryHelper.TryGetSignatureCollision(
            typeSymbol,
            context.Compilation,
            out var collisionDisplay))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.RecordConstructorOverloadSignatureCollision,
                typeLocation,
                typeSymbol.Name,
                collisionDisplay));
        }
    }

    private static void ReportNonparticipatingPropertyGuards(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        IReadOnlyList<IPropertySymbol> properties)
    {
        foreach (var property in properties)
        {
            if (RecordConstructorOverloadDiscoveryHelper.HasMarker(property))
                continue;

            foreach (var attribute in property.GetAttributes())
            {
                if (!IsConstructorGuardOrAlias(attribute))
                    continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardOnNonparticipatingProperty,
                    ConstructorGuardAnalysisHelper.GetAttributeLocation(
                        context,
                        attribute),
                    property.Name));
            }
        }
    }

    private static void AnalyzeParticipatingPropertyGuards(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol containingType,
        IPropertySymbol property)
    {
        foreach (var attribute in property.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass)
                continue;

            ConstructorGuardOccurrence occurrence;
            if (GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                attributeClass,
                ConstructorGuardAttributeName))
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

            ConstructorGuardAnalysisHelper.AnalyzePositiveGuardOccurrence(
                context,
                containingType,
                occurrence,
                ConstructorGuardAnalysisHelper.GetAttributeLocation(
                    context,
                    attribute));
        }
    }

    private static bool IsConstructorGuardOrAlias(AttributeData attribute)
    {
        if (attribute.AttributeClass is not { } attributeClass)
            return false;

        return GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                attributeClass,
                ConstructorGuardAttributeName) ||
            ConstructorGuardAnalysisHelper.TryGetGuardDefinition(
                attributeClass,
                out _,
                out _,
                out _);
    }
}
