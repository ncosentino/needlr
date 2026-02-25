using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that validates <c>[AgentHandoffsTo]</c> topology declarations.
/// </summary>
/// <remarks>
/// <para>
/// <b>NDLRMAF001</b> (Error): The target type referenced by <c>[AgentHandoffsTo(typeof(X))]</c> is not
/// decorated with <c>[NeedlrAiAgent]</c>. Handoff targets must be registered agent types.
/// </para>
/// <para>
/// <b>NDLRMAF003</b> (Warning): The class carrying <c>[AgentHandoffsTo]</c> is not itself decorated with
/// <c>[NeedlrAiAgent]</c>. The initial agent in a handoff workflow must be a declared agent.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentTopologyAnalyzer : DiagnosticAnalyzer
{
    private const string AgentHandoffsToAttributeName = "NexusLabs.Needlr.AgentFramework.AgentHandoffsToAttribute";
    private const string NeedlrAiAgentAttributeName = "NexusLabs.Needlr.AgentFramework.NeedlrAiAgentAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            MafDiagnosticDescriptors.HandoffsToTargetNotNeedlrAgent,
            MafDiagnosticDescriptors.HandoffsToSourceNotNeedlrAgent);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        var handoffsToAttrs = typeSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == AgentHandoffsToAttributeName)
            .ToImmutableArray();

        if (handoffsToAttrs.IsEmpty)
            return;

        // NDLRMAF003: source class lacks [NeedlrAiAgent]
        var hasNeedlrAiAgent = typeSymbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == NeedlrAiAgentAttributeName);

        if (!hasNeedlrAiAgent)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MafDiagnosticDescriptors.HandoffsToSourceNotNeedlrAgent,
                typeSymbol.Locations[0],
                typeSymbol.Name));
        }

        // NDLRMAF001: each target type must have [NeedlrAiAgent]
        foreach (var attr in handoffsToAttrs)
        {
            if (attr.ConstructorArguments.Length < 1)
                continue;

            var typeArg = attr.ConstructorArguments[0];
            if (typeArg.Kind != TypedConstantKind.Type || typeArg.Value is not INamedTypeSymbol targetType)
                continue;

            var targetHasNeedlrAiAgent = targetType.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == NeedlrAiAgentAttributeName);

            if (!targetHasNeedlrAiAgent)
            {
                // Report on the attribute usage location when available, otherwise fall back to the class
                var location = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                    ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                    : typeSymbol.Locations[0];

                context.ReportDiagnostic(Diagnostic.Create(
                    MafDiagnosticDescriptors.HandoffsToTargetNotNeedlrAgent,
                    location,
                    targetType.Name));
            }
        }
    }
}
