// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Analyzer that warns when [Options] classes use DataAnnotation validation attributes
/// that cannot be source-generated.
/// 
/// NDLRGEN030: DataAnnotation attribute cannot be source-generated
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsupportedDataAnnotationAnalyzer : DiagnosticAnalyzer
{
    private const string OptionsAttributeName = "OptionsAttribute";
    private const string GeneratorsNamespace = "NexusLabs.Needlr.Generators";
    private const string DataAnnotationsNamespace = "System.ComponentModel.DataAnnotations";

    private static readonly HashSet<string> SupportedDataAnnotations = new()
    {
        "RequiredAttribute",
        "RangeAttribute",
        "StringLengthAttribute",
        "MinLengthAttribute",
        "MaxLengthAttribute",
        "RegularExpressionAttribute",
        "EmailAddressAttribute",
        "PhoneAttribute",
        "UrlAttribute"
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.UnsupportedDataAnnotation);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeRecordDeclaration, SyntaxKind.RecordDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        AnalyzeTypeDeclaration(context, classDeclaration, classDeclaration.AttributeLists, classDeclaration.Members);
    }

    private static void AnalyzeRecordDeclaration(SyntaxNodeAnalysisContext context)
    {
        var recordDeclaration = (RecordDeclarationSyntax)context.Node;
        AnalyzeTypeDeclaration(context, recordDeclaration, recordDeclaration.AttributeLists, recordDeclaration.Members);
    }

    private static void AnalyzeTypeDeclaration(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax typeDeclaration,
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<MemberDeclarationSyntax> members)
    {
        // Check if this type has [Options] attribute
        if (!HasOptionsAttribute(context, attributeLists))
            return;

        var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
        if (typeSymbol == null)
            return;

        // Check all properties for unsupported DataAnnotations
        foreach (var member in members)
        {
            if (member is PropertyDeclarationSyntax property)
            {
                AnalyzeProperty(context, typeSymbol, property);
            }
        }
    }

    private static bool HasOptionsAttribute(SyntaxNodeAnalysisContext context, SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var attributeList in attributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbol = context.SemanticModel.GetSymbolInfo(attribute).Symbol?.ContainingType;
                if (symbol != null &&
                    symbol.Name == OptionsAttributeName &&
                    symbol.ContainingNamespace.ToDisplayString() == GeneratorsNamespace)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static void AnalyzeProperty(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol containingType,
        PropertyDeclarationSyntax property)
    {
        var propertySymbol = context.SemanticModel.GetDeclaredSymbol(property);
        if (propertySymbol == null)
            return;

        foreach (var attribute in propertySymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass == null)
                continue;

            var attrNamespace = attrClass.ContainingNamespace?.ToDisplayString() ?? "";
            var attrTypeName = attrClass.Name;

            // Only check DataAnnotations namespace
            if (attrNamespace != DataAnnotationsNamespace)
                continue;

            // Check if this is a ValidationAttribute (or subclass)
            if (!IsValidationAttribute(attrClass))
                continue;

            // Check if this is a supported attribute
            if (SupportedDataAnnotations.Contains(attrTypeName))
                continue;

            // Unsupported DataAnnotation - report diagnostic
            var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? property.GetLocation();
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.UnsupportedDataAnnotation,
                location,
                attrTypeName,
                containingType.Name,
                propertySymbol.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsValidationAttribute(INamedTypeSymbol attrClass)
    {
        var current = attrClass;
        while (current != null)
        {
            var fullName = current.ToDisplayString();
            if (fullName == "System.ComponentModel.DataAnnotations.ValidationAttribute")
                return true;
            current = current.BaseType;
        }
        return false;
    }
}
