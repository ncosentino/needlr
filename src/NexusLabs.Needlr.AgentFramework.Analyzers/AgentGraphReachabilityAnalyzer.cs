using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that detects agents in a graph that are not reachable from the entry point.
/// </summary>
/// <remarks>
/// <b>NDLRMAF022</b> (Warning): An agent declares edges in a named graph but is not reachable
/// from that graph's entry point via any path.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentGraphReachabilityAnalyzer : DiagnosticAnalyzer
{
    private const string AgentGraphEdgeAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGraphEdgeAttribute";
    private const string AgentGraphEntryAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGraphEntryAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.GraphUnreachableAgent);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // graphName → entryFqn
            var entryPoints = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
            // graphName → { sourceFqn → list of targetFqns }
            var graphEdges = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentBag<string>>>(StringComparer.Ordinal);
            // graphName → { fqn → (symbol, location) } for all edge source nodes
            var edgeSourceNodes = new ConcurrentDictionary<string, ConcurrentDictionary<string, (INamedTypeSymbol Symbol, Location Location)>>(StringComparer.Ordinal);

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;
                var fqn = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                foreach (var attr in typeSymbol.GetAttributes())
                {
                    var attrName = attr.AttributeClass?.ToDisplayString();

                    if (attrName == AgentGraphEntryAttributeName)
                    {
                        if (attr.ConstructorArguments.Length >= 1
                            && attr.ConstructorArguments[0].Value is string graphName)
                        {
                            entryPoints.TryAdd(graphName, fqn);
                        }
                    }

                    if (attrName == AgentGraphEdgeAttributeName)
                    {
                        if (attr.ConstructorArguments.Length < 2)
                            continue;

                        if (attr.ConstructorArguments[0].Value is not string graphName)
                            continue;

                        if (attr.ConstructorArguments[1].Kind != TypedConstantKind.Type
                            || attr.ConstructorArguments[1].Value is not INamedTypeSymbol targetType)
                            continue;

                        var targetFqn = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        var perGraph = graphEdges.GetOrAdd(graphName, _ => new ConcurrentDictionary<string, ConcurrentBag<string>>(StringComparer.Ordinal));
                        perGraph.GetOrAdd(fqn, _ => new ConcurrentBag<string>()).Add(targetFqn);

                        var location = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                            ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                            : typeSymbol.Locations[0];

                        var nodeMap = edgeSourceNodes.GetOrAdd(graphName, _ => new ConcurrentDictionary<string, (INamedTypeSymbol, Location)>(StringComparer.Ordinal));
                        nodeMap.TryAdd(fqn, (typeSymbol, location));
                    }
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var graphKvp in graphEdges)
                {
                    var graphName = graphKvp.Key;

                    if (!entryPoints.TryGetValue(graphName, out var entryFqn))
                        continue;

                    var adjacency = graphKvp.Value.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Distinct().ToList(),
                        StringComparer.Ordinal);

                    // BFS from entry point
                    var reachable = new HashSet<string>(StringComparer.Ordinal);
                    var queue = new Queue<string>();
                    queue.Enqueue(entryFqn);
                    reachable.Add(entryFqn);

                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        if (adjacency.TryGetValue(current, out var targets))
                        {
                            foreach (var target in targets)
                            {
                                if (reachable.Add(target))
                                {
                                    queue.Enqueue(target);
                                }
                            }
                        }
                    }

                    // Report unreachable edge-source nodes
                    if (!edgeSourceNodes.TryGetValue(graphName, out var nodeMap))
                        continue;

                    foreach (var nodeKvp in nodeMap)
                    {
                        if (!reachable.Contains(nodeKvp.Key))
                        {
                            endContext.ReportDiagnostic(Diagnostic.Create(
                                MafDiagnosticDescriptors.GraphUnreachableAgent,
                                nodeKvp.Value.Location,
                                nodeKvp.Value.Symbol.Name,
                                graphName));
                        }
                    }
                }
            });
        });
    }
}
