using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using NexusLabs.Needlr.Roslyn.Shared;

namespace NexusLabs.Needlr.SignalR.Analyzers;

/// <summary>
/// Analyzer that flags an <c>IHubRegistrationPlugin</c> implementation that is eligible
/// for Needlr's generated-constructor generation. The SignalR hub-registration generator
/// requires parameterless activation and deliberately excludes such a type from
/// registration, so a plugin shaped this way is silently never registered unless flagged
/// here.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HubRegistrationPluginConstructorAnalyzer : DiagnosticAnalyzer
{
    private const string HubRegistrationPluginInterfaceName = "NexusLabs.Needlr.SignalR.IHubRegistrationPlugin";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.HubRegistrationPluginRequiresParameterlessActivation);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol typeSymbol)
            return;

        if (!GeneratedConstructorEligibility.IsCanonicalDeclaration(typeSymbol, classDeclaration))
            return;

        if (!ImplementsHubRegistrationPlugin(typeSymbol))
            return;

        if (!GeneratedConstructorEligibility.IsEligibleForGeneratedConstructor(typeSymbol))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.HubRegistrationPluginRequiresParameterlessActivation,
            classDeclaration.Identifier.GetLocation(),
            typeSymbol.Name));
    }

    private static bool ImplementsHubRegistrationPlugin(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(i => i.ToDisplayString() == HubRegistrationPluginInterfaceName);
    }
}
