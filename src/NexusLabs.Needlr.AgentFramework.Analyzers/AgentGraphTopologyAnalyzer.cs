using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that validates graph edge and entry point declarations reference declared agents.
/// </summary>
/// <remarks>
/// <para>
/// <b>NDLRMAF019</b> (Error): An <c>[AgentGraphEdge]</c> target type is not decorated with
/// <c>[NeedlrAiAgent]</c>.
/// </para>
/// <para>
/// <b>NDLRMAF020</b> (Warning): A class has <c>[AgentGraphEdge]</c> but is not itself decorated
/// with <c>[NeedlrAiAgent]</c>.
/// </para>
/// <para>
/// <b>NDLRMAF021</b> (Warning): A class has <c>[AgentGraphEntry]</c> but is not itself decorated
/// with <c>[NeedlrAiAgent]</c>.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentGraphTopologyAnalyzer : DiagnosticAnalyzer
{
    private const string AgentGraphEdgeAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGraphEdgeAttribute";
    private const string AgentGraphEntryAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGraphEntryAttribute";
    private const string NeedlrAiAgentAttributeName = "NexusLabs.Needlr.AgentFramework.NeedlrAiAgentAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            MafDiagnosticDescriptors.GraphEdgeTargetNotAgent,
            MafDiagnosticDescriptors.GraphEdgeSourceNotAgent,
            MafDiagnosticDescriptors.GraphEntryPointNotAgent);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;
        var attributes = typeSymbol.GetAttributes();

        var hasNeedlrAiAgent = attributes
            .Any(a => a.AttributeClass?.ToDisplayString() == NeedlrAiAgentAttributeName);

        var graphEdgeAttrs = attributes
            .Where(a => a.AttributeClass?.ToDisplayString() == AgentGraphEdgeAttributeName)
            .ToImmutableArray();

        var graphEntryAttrs = attributes
            .Where(a => a.AttributeClass?.ToDisplayString() == AgentGraphEntryAttributeName)
            .ToImmutableArray();

        // NDLRMAF020: source class has [AgentGraphEdge] but no [NeedlrAiAgent]
        if (!graphEdgeAttrs.IsEmpty && !hasNeedlrAiAgent)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MafDiagnosticDescriptors.GraphEdgeSourceNotAgent,
                typeSymbol.Locations[0],
                typeSymbol.Name));
        }

        // NDLRMAF021: source class has [AgentGraphEntry] but no [NeedlrAiAgent]
        if (!graphEntryAttrs.IsEmpty && !hasNeedlrAiAgent)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MafDiagnosticDescriptors.GraphEntryPointNotAgent,
                typeSymbol.Locations[0],
                typeSymbol.Name));
        }

        // NDLRMAF019: each edge target must have [NeedlrAiAgent]
        foreach (var attr in graphEdgeAttrs)
        {
            if (attr.ConstructorArguments.Length < 2)
                continue;

            var targetTypeArg = attr.ConstructorArguments[1];
            if (targetTypeArg.Kind != TypedConstantKind.Type || targetTypeArg.Value is not INamedTypeSymbol targetType)
                continue;

            var targetHasNeedlrAiAgent = targetType.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == NeedlrAiAgentAttributeName);

            if (!targetHasNeedlrAiAgent)
            {
                var location = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                    ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                    : typeSymbol.Locations[0];

                context.ReportDiagnostic(Diagnostic.Create(
                    MafDiagnosticDescriptors.GraphEdgeTargetNotAgent,
                    location,
                    targetType.Name));
            }
        }
    }
}
