using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that detects terminal nodes that have outgoing edges.
/// </summary>
/// <remarks>
/// <b>NDLRMAF027</b> (Error): A node marked as terminal via
/// <c>[AgentGraphNode(IsTerminal = true)]</c> also has <c>[AgentGraphEdge]</c> declarations.
/// Terminal nodes must not have outgoing edges.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentGraphTerminalNodeAnalyzer : DiagnosticAnalyzer
{
    private const string AgentGraphEdgeAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGraphEdgeAttribute";
    private const string AgentGraphNodeAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGraphNodeAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.GraphTerminalNodeHasOutgoingEdges);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // Collect per-type: which graph names have IsTerminal=true, and which have outgoing edges
            // fqn → { graphName → (isTerminal, hasEdges, symbol, terminalLocation) }
            var nodeData = new ConcurrentDictionary<string, ConcurrentDictionary<string, (bool IsTerminal, Location TerminalLocation, bool HasEdges, INamedTypeSymbol Symbol)>>(
                StringComparer.Ordinal);

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;
                var fqn = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                // Track terminal markers per graph
                var terminalGraphs = new Dictionary<string, Location>(StringComparer.Ordinal);
                var edgeGraphs = new HashSet<string>(StringComparer.Ordinal);

                foreach (var attr in typeSymbol.GetAttributes())
                {
                    var attrName = attr.AttributeClass?.ToDisplayString();

                    if (attrName == AgentGraphNodeAttributeName)
                    {
                        if (attr.ConstructorArguments.Length < 1)
                            continue;

                        if (attr.ConstructorArguments[0].Value is not string graphName)
                            continue;

                        var isTerminal = false;
                        foreach (var namedArg in attr.NamedArguments)
                        {
                            if (namedArg.Key == "IsTerminal" && namedArg.Value.Value is bool val)
                            {
                                isTerminal = val;
                            }
                        }

                        if (isTerminal)
                        {
                            var location = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                                ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                                : typeSymbol.Locations[0];

                            terminalGraphs[graphName] = location;
                        }
                    }

                    if (attrName == AgentGraphEdgeAttributeName)
                    {
                        if (attr.ConstructorArguments.Length < 1)
                            continue;

                        if (attr.ConstructorArguments[0].Value is string graphName)
                        {
                            edgeGraphs.Add(graphName);
                        }
                    }
                }

                // Check for conflicts
                foreach (var kvp in terminalGraphs)
                {
                    if (edgeGraphs.Contains(kvp.Key))
                    {
                        var perNode = nodeData.GetOrAdd(fqn, _ => new ConcurrentDictionary<string, (bool, Location, bool, INamedTypeSymbol)>(StringComparer.Ordinal));
                        perNode[kvp.Key] = (true, kvp.Value, true, typeSymbol);
                    }
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var nodeKvp in nodeData)
                {
                    foreach (var graphKvp in nodeKvp.Value)
                    {
                        if (graphKvp.Value.IsTerminal && graphKvp.Value.HasEdges)
                        {
                            endContext.ReportDiagnostic(Diagnostic.Create(
                                MafDiagnosticDescriptors.GraphTerminalNodeHasOutgoingEdges,
                                graphKvp.Value.TerminalLocation,
                                graphKvp.Value.Symbol.Name,
                                graphKvp.Key));
                        }
                    }
                }
            });
        });
    }
}
