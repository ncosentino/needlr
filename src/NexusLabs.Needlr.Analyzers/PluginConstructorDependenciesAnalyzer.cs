using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that detects plugin implementations with constructor dependencies.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PluginConstructorDependenciesAnalyzer : DiagnosticAnalyzer
{
    // Plugin interfaces that are instantiated before DI is available
    private static readonly ImmutableHashSet<string> PluginInterfaceNames = ImmutableHashSet.Create(
        "IServiceCollectionPlugin",
        "IPostBuildServiceCollectionPlugin",
        "IWebApplicationBuilderPlugin",
        "IHostApplicationBuilderPlugin");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.PluginHasConstructorDependencies);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        // Skip abstract classes
        if (classDeclaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
        {
            return;
        }

        // Check if class implements a plugin interface
        if (!ImplementsPluginInterface(classDeclaration, context.SemanticModel))
        {
            return;
        }

        // Check constructors
        var constructors = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword))
            .ToList();

        // If no explicit constructors, there's an implicit parameterless constructor - OK
        if (constructors.Count == 0)
        {
            return;
        }

        // Check if there's at least one public parameterless constructor
        var hasParameterlessConstructor = constructors.Any(c =>
            c.Modifiers.Any(SyntaxKind.PublicKeyword) &&
            c.ParameterList.Parameters.Count == 0);

        if (hasParameterlessConstructor)
        {
            return;
        }

        // Report diagnostic on any constructor with parameters
        foreach (var constructor in constructors.Where(c => c.ParameterList.Parameters.Count > 0))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.PluginHasConstructorDependencies,
                constructor.Identifier.GetLocation(),
                classDeclaration.Identifier.Text);

            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// True when <paramref name="classDeclaration"/> lists a Needlr plugin interface,
    /// directly or through any base type or inherited interface.
    /// </summary>
    /// <remarks>
    /// Matching is exclusively symbol based: a base-list entry the semantic model cannot
    /// resolve to a type at all is skipped rather than matched on its simple name, since
    /// a bare <c>IServiceCollectionPlugin</c> identifier in incomplete code may just as
    /// easily refer to an unrelated same-named interface from another namespace. An
    /// unresolved but namespace-qualified entry (an error symbol still carrying the
    /// <c>NexusLabs.Needlr</c> namespace and a plugin interface name) is unambiguous and
    /// is still diagnosed.
    /// </remarks>
    private static bool ImplementsPluginInterface(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel)
    {
        if (classDeclaration.BaseList == null)
        {
            return false;
        }

        foreach (var baseType in classDeclaration.BaseList.Types)
        {
            var typeInfo = semanticModel.GetTypeInfo(baseType.Type);
            var typeSymbol = typeInfo.Type;

            if (typeSymbol == null)
            {
                continue;
            }

            // Check the type and all its interfaces
            if (IsPluginInterface(typeSymbol))
            {
                return true;
            }

            foreach (var iface in typeSymbol.AllInterfaces)
            {
                if (IsPluginInterface(iface))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPluginInterface(ITypeSymbol typeSymbol)
    {
        return PluginInterfaceNames.Contains(typeSymbol.Name) &&
               typeSymbol.ContainingNamespace?.ToString() == "NexusLabs.Needlr";
    }
}
