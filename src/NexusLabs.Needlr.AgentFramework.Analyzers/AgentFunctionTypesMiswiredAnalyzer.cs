using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that detects types listed in <c>FunctionTypes</c> on <c>[NeedlrAiAgent]</c> that
/// have no <c>[AgentFunction]</c> methods, causing the agent to silently receive zero tools.
/// </summary>
/// <remarks>
/// <b>NDLRMAF014</b> (Warning): A type in <c>FunctionTypes</c> has no <c>[AgentFunction]</c> methods.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentFunctionTypesMiswiredAnalyzer : DiagnosticAnalyzer
{
    private const string NeedlrAiAgentAttributeName = "NexusLabs.Needlr.AgentFramework.NeedlrAiAgentAttribute";
    private const string AgentFunctionAttributeName = "NexusLabs.Needlr.AgentFramework.AgentFunctionAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.AgentFunctionTypesMiswired);

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
            if (attr.AttributeClass?.ToDisplayString() != NeedlrAiAgentAttributeName)
                continue;

            var functionTypesArg = attr.NamedArguments
                .FirstOrDefault(n => n.Key == "FunctionTypes");

            if (functionTypesArg.Key is null)
                continue;

            if (functionTypesArg.Value.Kind != TypedConstantKind.Array)
                continue;

            foreach (var element in functionTypesArg.Value.Values)
            {
                if (element.Kind != TypedConstantKind.Type)
                    continue;

                if (element.Value is not INamedTypeSymbol functionType)
                    continue;

                var hasAgentFunction = HasAnyAgentFunctionMethod(functionType);
                if (hasAgentFunction)
                    continue;

                var location = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                    ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                    : typeSymbol.Locations[0];

                context.ReportDiagnostic(Diagnostic.Create(
                    MafDiagnosticDescriptors.AgentFunctionTypesMiswired,
                    location,
                    functionType.Name,
                    typeSymbol.Name));
            }
        }
    }

    private static bool HasAnyAgentFunctionMethod(INamedTypeSymbol type)
    {
        return type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(m => m.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == AgentFunctionAttributeName));
    }
}
