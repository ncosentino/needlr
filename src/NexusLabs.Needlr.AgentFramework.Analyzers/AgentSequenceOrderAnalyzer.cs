using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that validates <c>Order</c> values within <c>[AgentSequenceMember]</c> pipeline declarations.
/// </summary>
/// <remarks>
/// <para>
/// <b>NDLRMAF006</b> (Error): Two or more agents in the same pipeline declare the same <c>Order</c> value.
/// </para>
/// <para>
/// <b>NDLRMAF007</b> (Warning): The <c>Order</c> values in a pipeline are not contiguous — a gap exists.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentSequenceOrderAnalyzer : DiagnosticAnalyzer
{
    private const string AgentSequenceMemberAttributeName = "NexusLabs.Needlr.AgentFramework.AgentSequenceMemberAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            MafDiagnosticDescriptors.DuplicateSequenceOrder,
            MafDiagnosticDescriptors.GapInSequenceOrder);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // pipeline name → list of (agent type name, order, attribute location)
            var pipelineEntries = new ConcurrentDictionary<string, ConcurrentBag<(string AgentName, int Order, Location Location)>>(
                StringComparer.Ordinal);

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;

                foreach (var attr in typeSymbol.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() != AgentSequenceMemberAttributeName)
                        continue;

                    if (attr.ConstructorArguments.Length < 2)
                        continue;

                    if (attr.ConstructorArguments[0].Value is not string pipelineName
                        || string.IsNullOrWhiteSpace(pipelineName))
                        continue;

                    if (attr.ConstructorArguments[1].Value is not int order)
                        continue;

                    var attrLocation = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                        ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                        : typeSymbol.Locations[0];

                    pipelineEntries.GetOrAdd(pipelineName, _ => new ConcurrentBag<(string, int, Location)>())
                        .Add((typeSymbol.Name, order, attrLocation));
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var kvp in pipelineEntries)
                {
                    var pipelineName = kvp.Key;
                    var entries = kvp.Value.ToList();

                    // NDLRMAF006: duplicate Order values
                    var duplicateGroups = entries.GroupBy(e => e.Order).Where(g => g.Count() > 1).ToList();
                    foreach (var group in duplicateGroups)
                    {
                        foreach (var (agentName, order, location) in group)
                        {
                            endContext.ReportDiagnostic(Diagnostic.Create(
                                MafDiagnosticDescriptors.DuplicateSequenceOrder,
                                location,
                                pipelineName,
                                order,
                                agentName));
                        }
                    }

                    // NDLRMAF007: gap in Order sequence — only when no duplicate errors and 2+ members
                    if (duplicateGroups.Count == 0 && entries.Count >= 2)
                    {
                        var sortedOrders = entries.Select(e => e.Order).OrderBy(o => o).ToList();
                        for (var i = 1; i < sortedOrders.Count; i++)
                        {
                            if (sortedOrders[i] != sortedOrders[i - 1] + 1)
                            {
                                var missingOrder = sortedOrders[i - 1] + 1;
                                foreach (var (_, _, location) in entries)
                                {
                                    endContext.ReportDiagnostic(Diagnostic.Create(
                                        MafDiagnosticDescriptors.GapInSequenceOrder,
                                        location,
                                        pipelineName,
                                        missingOrder));
                                }
                                break; // Report the first gap only per pipeline
                            }
                        }
                    }
                }
            });
        });
    }
}
