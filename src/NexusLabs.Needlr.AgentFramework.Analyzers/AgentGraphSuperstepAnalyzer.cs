using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that validates the <c>MaxSupersteps</c> property on <c>[AgentGraphEntry]</c>.
/// </summary>
/// <remarks>
/// <b>NDLRMAF023</b> (Error): <c>MaxSupersteps</c> is set to a value ≤ 0, which prevents
/// the graph from making any progress.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentGraphSuperstepAnalyzer : DiagnosticAnalyzer
{
    private const string AgentGraphEntryAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGraphEntryAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.GraphInvalidMaxSupersteps);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != AgentGraphEntryAttributeName)
                continue;

            if (attr.ConstructorArguments.Length < 1)
                continue;

            if (attr.ConstructorArguments[0].Value is not string graphName)
                continue;

            // Check the MaxSupersteps named argument
            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key != "MaxSupersteps")
                    continue;

                if (namedArg.Value.Value is int maxSupersteps && maxSupersteps <= 0)
                {
                    var location = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                        ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                        : typeSymbol.Locations[0];

                    context.ReportDiagnostic(Diagnostic.Create(
                        MafDiagnosticDescriptors.GraphInvalidMaxSupersteps,
                        location,
                        graphName,
                        maxSupersteps));
                }
            }
        }
    }
}
