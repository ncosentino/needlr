using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that detects <c>[AgentFunction]</c> methods and their parameters that are
/// missing <c>[System.ComponentModel.Description]</c> attributes.
/// </summary>
/// <remarks>
/// <b>NDLRMAF012</b> (Warning): An <c>[AgentFunction]</c> method has no <c>[Description]</c>.<br/>
/// <b>NDLRMAF013</b> (Warning): A non-<c>CancellationToken</c> parameter of an <c>[AgentFunction]</c>
/// method has no <c>[Description]</c>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentFunctionDescriptionAnalyzer : DiagnosticAnalyzer
{
    private const string AgentFunctionAttributeName = "NexusLabs.Needlr.AgentFramework.AgentFunctionAttribute";
    private const string DescriptionAttributeName = "System.ComponentModel.DescriptionAttribute";
    private const string CancellationTokenTypeName = "System.Threading.CancellationToken";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            MafDiagnosticDescriptors.AgentFunctionMissingDescription,
            MafDiagnosticDescriptors.AgentFunctionParameterMissingDescription);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        var agentFunctionAttr = method.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AgentFunctionAttributeName);

        if (agentFunctionAttr is null)
            return;

        var attrLocation = agentFunctionAttr.ApplicationSyntaxReference?.SyntaxTree is { } tree
            ? Location.Create(tree, agentFunctionAttr.ApplicationSyntaxReference.Span)
            : method.Locations[0];

        var hasDescription = method.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == DescriptionAttributeName);

        if (!hasDescription)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MafDiagnosticDescriptors.AgentFunctionMissingDescription,
                attrLocation,
                method.Name));
        }

        foreach (var parameter in method.Parameters)
        {
            if (parameter.Type.ToDisplayString() == CancellationTokenTypeName)
                continue;

            var paramHasDescription = parameter.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == DescriptionAttributeName);

            if (!paramHasDescription)
            {
                var paramLocation = parameter.Locations.FirstOrDefault() ?? method.Locations[0];

                context.ReportDiagnostic(Diagnostic.Create(
                    MafDiagnosticDescriptors.AgentFunctionParameterMissingDescription,
                    paramLocation,
                    parameter.Name,
                    method.Name));
            }
        }
    }
}
