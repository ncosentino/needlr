using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that detects cycles in agent graphs declared via <c>[AgentGraphEdge]</c>.
/// </summary>
/// <remarks>
/// <b>NDLRMAF016</b> (Error): A cycle was found in the agent graph — agent graphs must be
/// directed acyclic graphs (DAGs).
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentGraphCycleAnalyzer : DiagnosticAnalyzer
{
    private const string AgentGraphEdgeAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGraphEdgeAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.GraphCycleDetected);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // graphName → { sourceFqn → list of (targetFqn, sourceSymbol, location) }
            var graphEdges = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentBag<(string TargetFqn, INamedTypeSymbol SourceSymbol, Location Location)>>>(
                StringComparer.Ordinal);

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;
                var sourceFqn = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                foreach (var attr in typeSymbol.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() != AgentGraphEdgeAttributeName)
                        continue;

                    if (attr.ConstructorArguments.Length < 2)
                        continue;

                    var graphNameArg = attr.ConstructorArguments[0];
                    if (graphNameArg.Value is not string graphName)
                        continue;

                    var targetTypeArg = attr.ConstructorArguments[1];
                    if (targetTypeArg.Kind != TypedConstantKind.Type || targetTypeArg.Value is not INamedTypeSymbol targetType)
                        continue;

                    var targetFqn = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var attrLocation = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                        ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                        : typeSymbol.Locations[0];

                    var perGraph = graphEdges.GetOrAdd(graphName, _ => new ConcurrentDictionary<string, ConcurrentBag<(string, INamedTypeSymbol, Location)>>(StringComparer.Ordinal));
                    perGraph.GetOrAdd(sourceFqn, _ => new ConcurrentBag<(string, INamedTypeSymbol, Location)>())
                        .Add((targetFqn, typeSymbol, attrLocation));
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var graphKvp in graphEdges)
                {
                    var graphName = graphKvp.Key;
                    var edges = graphKvp.Value;

                    var adjacency = edges.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Select(e => e.TargetFqn).Distinct().ToList(),
                        StringComparer.Ordinal);

                    var symbolMap = new Dictionary<string, (INamedTypeSymbol Symbol, List<Location> Locations)>(StringComparer.Ordinal);
                    foreach (var kvp in edges)
                    {
                        var entries = kvp.Value.ToList();
                        symbolMap[kvp.Key] = (entries[0].SourceSymbol, entries.Select(e => e.Location).ToList());
                    }

                    var visited = new HashSet<string>(StringComparer.Ordinal);
                    var reported = new HashSet<string>(StringComparer.Ordinal);

                    foreach (var start in adjacency.Keys)
                    {
                        if (visited.Contains(start))
                            continue;

                        var path = new List<string>();
                        DetectCycle(start, adjacency, visited, path, reported, endContext, symbolMap, graphName);
                    }
                }
            });
        });
    }

    private static void DetectCycle(
        string current,
        Dictionary<string, List<string>> adjacency,
        HashSet<string> visited,
        List<string> path,
        HashSet<string> reported,
        CompilationAnalysisContext context,
        Dictionary<string, (INamedTypeSymbol Symbol, List<Location> Locations)> symbolMap,
        string graphName)
    {
        path.Add(current);

        if (adjacency.TryGetValue(current, out var neighbors))
        {
            foreach (var next in neighbors)
            {
                var cycleIndex = path.IndexOf(next);
                if (cycleIndex >= 0)
                {
                    var cycleNodes = path.Skip(cycleIndex).Concat(new[] { next }).ToList();
                    var cycleKey = string.Join(" \u2192 ", cycleNodes.Select(ShortName));

                    foreach (var node in cycleNodes.Take(cycleNodes.Count - 1))
                    {
                        if (!reported.Add(node))
                            continue;

                        if (!symbolMap.TryGetValue(node, out var info))
                            continue;

                        foreach (var location in info.Locations)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                MafDiagnosticDescriptors.GraphCycleDetected,
                                location,
                                graphName,
                                cycleKey));
                        }
                    }
                }
                else if (!visited.Contains(next))
                {
                    DetectCycle(next, adjacency, visited, path, reported, context, symbolMap, graphName);
                }
            }
        }

        path.RemoveAt(path.Count - 1);
        visited.Add(current);
    }

    private static string ShortName(string fqn)
    {
        var clean = fqn.StartsWith("global::") ? fqn.Substring(8) : fqn;
        var dot = clean.LastIndexOf('.');
        return dot >= 0 ? clean.Substring(dot + 1) : clean;
    }
}
