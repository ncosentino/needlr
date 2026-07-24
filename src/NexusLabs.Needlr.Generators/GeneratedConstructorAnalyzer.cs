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
/// Validates generated-constructor type shapes, field participation, built-in and
/// custom guards, and custom guard alias definitions.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeneratedConstructorAnalyzer : DiagnosticAnalyzer
{
    private const string GenerateConstructorAttributeName = "GenerateConstructorAttribute";
    private const string ConstructorGuardAttributeName = "ConstructorGuardAttribute";
    private const string ConstructorIgnoreAttributeName = "ConstructorIgnoreAttribute";
    private const string ConstructorGuardDefinitionAttributeName = "ConstructorGuardDefinitionAttribute";
    private const string DefaultGuardMethodName = "Validate";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.GeneratedConstructorRequiresPartialType,
            DiagnosticDescriptors.GeneratedConstructorUnsupportedTypeShape,
            DiagnosticDescriptors.GeneratedConstructorConflictsWithExplicitConstructor,
            DiagnosticDescriptors.GeneratedConstructorBaseTypeRequiresParameterlessConstructor,
            DiagnosticDescriptors.GeneratedConstructorNoEligibleFields,
            DiagnosticDescriptors.GeneratedConstructorParameterNameCollision,
            DiagnosticDescriptors.ConstructorGuardAttributeHasNoEffect,
            DiagnosticDescriptors.ConstructorGuardAttributeOnIneligibleField,
            DiagnosticDescriptors.InvalidConstructorGuardEnumValue,
            DiagnosticDescriptors.ConstructorGuardIncompatibleWithFieldType,
            DiagnosticDescriptors.ConstructorGuardTypeInvalid,
            DiagnosticDescriptors.ConstructorGuardMethodNameInvalid,
            DiagnosticDescriptors.ConstructorGuardMethodInvalid,
            DiagnosticDescriptors.ConstructorGuardMethodAmbiguous,
            DiagnosticDescriptors.ConstructorGuardDefinitionTargetInvalid,
            DiagnosticDescriptors.ConstructorGuardDefinitionUnresolvedGuard,
            DiagnosticDescriptors.ConstructorGuardAliasUsageArgumentUnsupported,
            DiagnosticDescriptors.ConstructorGuardForwardedArgumentIncompatible);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeTypeDeclaration,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.RecordDeclaration);
    }

    private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(typeDeclaration) is not
            INamedTypeSymbol typeSymbol)
        {
            return;
        }

        var occurrences = CollectFieldGuardOccurrences(
            context,
            typeDeclaration);
        var classHasTrigger =
            GeneratedConstructorEligibility.HasGenerateConstructorAttribute(typeSymbol) ||
            GeneratedConstructorEligibility.HasPositiveFieldGuardTrigger(typeSymbol);

        ReportFieldOccurrenceDiagnostics(
            context,
            typeSymbol,
            occurrences,
            classHasTrigger);

        if (!GeneratedConstructorEligibility.IsCanonicalDeclaration(
            typeSymbol,
            typeDeclaration))
        {
            return;
        }

        AnalyzeGuardDefinitionTarget(context, typeSymbol);

        if (!classHasTrigger)
            return;

        if (typeSymbol.IsRecord &&
            RecordConstructorOverloadDiscoveryHelper
                .GetMarkedProperties(typeSymbol).Count > 0)
        {
            return;
        }

        AnalyzeGenerateConstructorEnumArgument(context, typeSymbol);
        AnalyzeTypeShape(context, typeDeclaration, typeSymbol);
    }

    private static void AnalyzeTypeShape(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax typeDeclaration,
        INamedTypeSymbol typeSymbol)
    {
        var location = typeDeclaration.Identifier.GetLocation();

        if (typeSymbol.IsRecord)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedConstructorUnsupportedTypeShape,
                location,
                typeSymbol.Name,
                "a record type"));
        }
        else if (typeSymbol.ContainingType is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedConstructorUnsupportedTypeShape,
                location,
                typeSymbol.Name,
                "a nested type"));
        }

        if (!GeneratedConstructorEligibility.IsDeclaredPartial(typeSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedConstructorRequiresPartialType,
                location,
                typeSymbol.Name));
        }

        if (GeneratedConstructorEligibility.HasExplicitInstanceConstructor(typeSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedConstructorConflictsWithExplicitConstructor,
                location,
                typeSymbol.Name));
        }

        if (typeSymbol.BaseType is not null &&
            typeSymbol.BaseType.SpecialType != SpecialType.System_Object &&
            !GeneratedConstructorEligibility.HasAccessibleParameterlessConstructor(
                typeSymbol.BaseType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedConstructorBaseTypeRequiresParameterlessConstructor,
                location,
                typeSymbol.Name,
                typeSymbol.BaseType.Name));
        }

        var eligibleFields =
            GeneratedConstructorEligibility.GetEligibleConstructorFields(typeSymbol);
        if (eligibleFields.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedConstructorNoEligibleFields,
                location,
                typeSymbol.Name));
            return;
        }

        var seenParameterNames = new HashSet<string>(
            System.StringComparer.Ordinal);
        foreach (var field in eligibleFields)
        {
            var parameterName =
                ConstructorGenerationDiscoveryHelper.GetParameterName(field.Name);
            if (!seenParameterNames.Add(parameterName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.GeneratedConstructorParameterNameCollision,
                    location,
                    typeSymbol.Name,
                    parameterName));
            }
        }
    }

    private static void AnalyzeGenerateConstructorEnumArgument(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attrClass ||
                !GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                    attrClass,
                    GenerateConstructorAttributeName) ||
                attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            ConstructorGuardAnalysisHelper.TryReportUndefinedEnum(
                context,
                ConstructorGuardAnalysisHelper.GetAttributeLocation(
                    context,
                    attribute),
                attribute.ConstructorArguments[0]);
        }
    }

    private static List<ConstructorGuardOccurrence> CollectFieldGuardOccurrences(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax typeDeclaration)
    {
        var occurrences = new List<ConstructorGuardOccurrence>();

        foreach (var fieldDeclaration in typeDeclaration.Members
            .OfType<FieldDeclarationSyntax>())
        {
            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                if (context.SemanticModel.GetDeclaredSymbol(variable) is not
                    IFieldSymbol fieldSymbol)
                {
                    continue;
                }

                var ineligibilityReason =
                    GeneratedConstructorEligibility.GetFieldIneligibilityReason(
                        fieldSymbol);

                foreach (var attribute in fieldSymbol.GetAttributes())
                {
                    if (attribute.AttributeClass is not { } attrClass)
                        continue;

                    if (GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                        attrClass,
                        ConstructorIgnoreAttributeName))
                    {
                        occurrences.Add(new ConstructorGuardOccurrence(
                            fieldSymbol,
                            fieldSymbol.Type,
                            "field",
                            attribute,
                            ConstructorGuardOccurrenceKind.Ignore,
                            ineligibilityReason,
                            null,
                            null,
                            false,
                            false));
                        continue;
                    }

                    if (GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                        attrClass,
                        ConstructorGuardAttributeName))
                    {
                        occurrences.Add(
                            ConstructorGuardAnalysisHelper.BuildDirectGuardOccurrence(
                                fieldSymbol,
                                fieldSymbol.Type,
                                "field",
                                attribute,
                                ineligibilityReason));
                        continue;
                    }

                    if (!ConstructorGuardAnalysisHelper.TryGetGuardDefinition(
                        attrClass,
                        out var guardType,
                        out var methodName,
                        out var methodNameExplicit))
                    {
                        continue;
                    }

                    occurrences.Add(
                        ConstructorGuardAnalysisHelper.BuildAliasOccurrence(
                            fieldSymbol,
                            fieldSymbol.Type,
                            "field",
                            attribute,
                            ineligibilityReason,
                            guardType,
                            methodName,
                            methodNameExplicit,
                            attrClass.DeclaringSyntaxReferences.Length > 0));
                }
            }
        }

        return occurrences;
    }

    private static void ReportFieldOccurrenceDiagnostics(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        IReadOnlyList<ConstructorGuardOccurrence> occurrences,
        bool classHasTrigger)
    {
        foreach (var occurrence in occurrences)
        {
            var location =
                ConstructorGuardAnalysisHelper.GetAttributeLocation(
                    context,
                    occurrence.Attribute);

            if (occurrence.IneligibilityReason is { } reason)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardAttributeOnIneligibleField,
                    location,
                    occurrence.Member.Name,
                    reason));
                continue;
            }

            if (occurrence.Kind == ConstructorGuardOccurrenceKind.Ignore &&
                !classHasTrigger)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardAttributeHasNoEffect,
                    location,
                    occurrence.Member.Name,
                    "[ConstructorIgnore]"));
                continue;
            }

            if (occurrence.Kind ==
                    ConstructorGuardOccurrenceKind.BuiltInNone &&
                !classHasTrigger)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardAttributeHasNoEffect,
                    location,
                    occurrence.Member.Name,
                    "[ConstructorGuard(ConstructorGuardKind.None)]"));
                continue;
            }

            ConstructorGuardAnalysisHelper.AnalyzePositiveGuardOccurrence(
                context,
                typeSymbol,
                occurrence,
                location);
        }
    }

    private static void AnalyzeGuardDefinitionTarget(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attrClass ||
                !GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                    attrClass,
                    ConstructorGuardDefinitionAttributeName))
            {
                continue;
            }

            var location =
                ConstructorGuardAnalysisHelper.GetAttributeLocation(
                    context,
                    attribute);
            var targetInvalidReason =
                ConstructorGuardAnalysisHelper.GetGuardDefinitionTargetInvalidReason(
                    typeSymbol);
            if (targetInvalidReason is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardDefinitionTargetInvalid,
                    location,
                    typeSymbol.Name,
                    targetInvalidReason));
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Value is not ITypeSymbol guardType ||
                guardType.TypeKind == TypeKind.Error)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardDefinitionUnresolvedGuard,
                    location,
                    typeSymbol.Name,
                    "the guard type could not be resolved"));
                continue;
            }

            if (!context.Compilation.IsSymbolAccessibleWithin(
                guardType,
                typeSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardDefinitionUnresolvedGuard,
                    location,
                    typeSymbol.Name,
                    $"'{guardType.ToDisplayString()}' is not accessible from '{typeSymbol.ToDisplayString()}'"));
                continue;
            }

            var methodNameExplicit =
                attribute.ConstructorArguments.Length > 1 &&
                attribute.ConstructorArguments[1].Value is string;
            var methodName = methodNameExplicit
                ? (string)attribute.ConstructorArguments[1].Value!
                : DefaultGuardMethodName;
            if (methodNameExplicit && string.IsNullOrWhiteSpace(methodName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardDefinitionUnresolvedGuard,
                    location,
                    typeSymbol.Name,
                    "the guard method name must not be empty or consist only of white space"));
                continue;
            }

            var resolution =
                ConstructorGuardAnalysisHelper.TryResolveGuardMethod(
                    context.Compilation,
                    typeSymbol,
                    guardType,
                    methodName,
                    null,
                    "member",
                    ImmutableArray<ITypeSymbol>.Empty,
                    out _,
                    out var reason,
                    out _);
            if (resolution == GuardMethodResolution.NotFound)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardDefinitionUnresolvedGuard,
                    location,
                    typeSymbol.Name,
                    reason ?? "no compatible guard method was found"));
            }
        }
    }
}
