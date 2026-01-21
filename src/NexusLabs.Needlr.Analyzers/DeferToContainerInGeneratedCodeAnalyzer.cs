using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that detects [DeferToContainer] attributes placed in generated code.
/// </summary>
/// <remarks>
/// <para>
/// Source generators run in isolation and cannot see output from other generators.
/// If another generator adds [DeferToContainer] to a partial class, Needlr's
/// TypeRegistryGenerator will not see it and will generate incorrect factory code.
/// </para>
/// <para>
/// This analyzer runs after all generators complete and can detect this scenario,
/// warning users to move the attribute to their original source file.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeferToContainerInGeneratedCodeAnalyzer : DiagnosticAnalyzer
{
    private const string DeferToContainerAttributeName = "DeferToContainerAttribute";
    private const string DeferToContainerAttributeShortName = "DeferToContainer";
    private const string DeferToContainerAttributeFullName = "NexusLabs.Needlr.DeferToContainerAttribute";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.DeferToContainerInGeneratedCode);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        // Important: We WANT to analyze generated code - that's the whole point!
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        // Check if this class has [DeferToContainer] attribute
        var deferToContainerAttribute = FindDeferToContainerAttribute(classDeclaration);
        if (deferToContainerAttribute == null)
        {
            return;
        }

        // Check if the attribute is in generated code
        if (!IsInGeneratedCode(deferToContainerAttribute, context))
        {
            return;
        }

        // Report diagnostic on the attribute
        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.DeferToContainerInGeneratedCode,
            deferToContainerAttribute.GetLocation(),
            classDeclaration.Identifier.Text);

        context.ReportDiagnostic(diagnostic);
    }

    private static AttributeSyntax? FindDeferToContainerAttribute(ClassDeclarationSyntax classDeclaration)
    {
        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = GetAttributeName(attribute);
                if (name == DeferToContainerAttributeName ||
                    name == DeferToContainerAttributeShortName)
                {
                    return attribute;
                }
            }
        }

        return null;
    }

    private static string? GetAttributeName(AttributeSyntax attribute)
    {
        return attribute.Name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            _ => null
        };
    }

    private static bool IsInGeneratedCode(SyntaxNode node, SyntaxNodeAnalysisContext context)
    {
        var syntaxTree = node.SyntaxTree;

        // Check file path for common generated code patterns
        var filePath = syntaxTree.FilePath;
        if (!string.IsNullOrEmpty(filePath))
        {
            // Common generated file patterns
            if (filePath.EndsWith(".g.cs") ||
                filePath.EndsWith(".generated.cs") ||
                filePath.EndsWith(".designer.cs"))
            {
                return true;
            }

            // Check for obj/generated folder (common for source generators)
            var normalizedPath = filePath.Replace('\\', '/').ToLowerInvariant();
            if (normalizedPath.Contains("/obj/") && normalizedPath.Contains("/generated/"))
            {
                return true;
            }
        }

        // Check for [GeneratedCode] attribute on the class
        var classDeclaration = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDeclaration != null)
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
            if (symbol != null && HasGeneratedCodeAttribute(symbol))
            {
                return true;
            }
        }

        // Check if the syntax tree is marked as generated
        if (context.SemanticModel.Compilation.Options.SyntaxTreeOptionsProvider != null)
        {
            // The tree options provider can indicate generated code status
            // but we've already covered the main cases above
        }

        return false;
    }

    private static bool HasGeneratedCodeAttribute(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass == null)
            {
                continue;
            }

            // Check for System.CodeDom.Compiler.GeneratedCodeAttribute
            if (attrClass.Name == "GeneratedCodeAttribute" &&
                attrClass.ContainingNamespace?.ToDisplayString() == "System.CodeDom.Compiler")
            {
                return true;
            }

            // Also check for CompilerGeneratedAttribute
            if (attrClass.Name == "CompilerGeneratedAttribute" &&
                attrClass.ContainingNamespace?.ToDisplayString() == "System.Runtime.CompilerServices")
            {
                return true;
            }
        }

        return false;
    }
}
