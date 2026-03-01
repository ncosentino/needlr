using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that warns when [DoNotAutoRegister] is applied directly to a class
/// that implements a Needlr plugin interface.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DoNotAutoRegisterOnPluginAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> PluginInterfaceNames = ImmutableHashSet.Create(
        "IServiceCollectionPlugin",
        "IPostBuildServiceCollectionPlugin",
        "IWebApplicationPlugin",
        "IWebApplicationBuilderPlugin",
        "IHostApplicationBuilderPlugin");

    private const string DoNotAutoRegisterAttributeName = "DoNotAutoRegisterAttribute";
    private const string DoNotAutoRegisterShortName = "DoNotAutoRegister";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.DoNotAutoRegisterOnPluginClass);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (!HasDoNotAutoRegisterDirectly(classDeclaration, context.SemanticModel, out var attributeLocation))
            return;

        if (!ImplementsPluginInterface(classDeclaration, context.SemanticModel))
            return;

        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.DoNotAutoRegisterOnPluginClass,
            attributeLocation,
            classDeclaration.Identifier.Text);

        context.ReportDiagnostic(diagnostic);
    }

    private static bool HasDoNotAutoRegisterDirectly(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        out Location? attributeLocation)
    {
        attributeLocation = null;
        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = attribute.Name.ToString();
                if (name == DoNotAutoRegisterShortName || name == DoNotAutoRegisterAttributeName ||
                    name.EndsWith("." + DoNotAutoRegisterShortName) || name.EndsWith("." + DoNotAutoRegisterAttributeName))
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(attribute);
                    var attributeClass = symbolInfo.Symbol?.ContainingType
                        ?? symbolInfo.CandidateSymbols.FirstOrDefault()?.ContainingType;

                    if (attributeClass != null)
                    {
                        var fullName = attributeClass.ToDisplayString();
                        if (fullName != "NexusLabs.Needlr.DoNotAutoRegisterAttribute")
                            continue;
                    }

                    attributeLocation = attributeList.GetLocation();
                    return true;
                }
            }
        }
        return false;
    }

    private static bool ImplementsPluginInterface(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel)
    {
        if (classDeclaration.BaseList == null)
            return false;

        if (semanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
            return false;

        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (PluginInterfaceNames.Contains(iface.Name) &&
                iface.ContainingNamespace?.ToString() == "NexusLabs.Needlr")
            {
                return true;
            }
        }

        return false;
    }
}
