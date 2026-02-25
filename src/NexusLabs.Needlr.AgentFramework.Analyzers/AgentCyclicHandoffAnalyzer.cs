using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that detects cyclic handoff chains in <c>[AgentHandoffsTo]</c> topology declarations.
/// </summary>
/// <remarks>
/// <b>NDLRMAF004</b> (Warning): A cycle was found in the agent handoff graph — for example A → B → A.
/// While MAF may handle runtime termination conditions, a cycle is almost always a topology design error.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentCyclicHandoffAnalyzer : DiagnosticAnalyzer
{
    private const string AgentHandoffsToAttributeName = "NexusLabs.Needlr.AgentFramework.AgentHandoffsToAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.CyclicHandoffChain);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // source FQN → list of (target FQN, source type symbol, attribute location)
            var edges = new ConcurrentDictionary<string, ConcurrentBag<(string TargetFqn, INamedTypeSymbol SourceSymbol, Location Location)>>(
                StringComparer.Ordinal);

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;
                var sourceFqn = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                foreach (var attr in typeSymbol.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() != AgentHandoffsToAttributeName)
                        continue;

                    if (attr.ConstructorArguments.Length < 1)
                        continue;

                    var typeArg = attr.ConstructorArguments[0];
                    if (typeArg.Kind != TypedConstantKind.Type || typeArg.Value is not INamedTypeSymbol targetType)
                        continue;

                    var targetFqn = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var attrLocation = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                        ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                        : typeSymbol.Locations[0];

                    edges.GetOrAdd(sourceFqn, _ => new ConcurrentBag<(string, INamedTypeSymbol, Location)>())
                        .Add((targetFqn, typeSymbol, attrLocation));
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                // Build adjacency list: FQN → set of target FQNs
                var adjacency = edges.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(e => e.TargetFqn).Distinct().ToList(),
                    StringComparer.Ordinal);

                // Map FQN → (source symbol, attribute locations) for diagnostics
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
                    DetectCycle(start, adjacency, visited, path, reported, endContext, symbolMap);
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
        Dictionary<string, (INamedTypeSymbol Symbol, List<Location> Locations)> symbolMap)
    {
        path.Add(current);

        if (adjacency.TryGetValue(current, out var neighbors))
        {
            foreach (var next in neighbors)
            {
                var cycleIndex = path.IndexOf(next);
                if (cycleIndex >= 0)
                {
                    // Found a cycle: path[cycleIndex..] + next
                    var cycleNodes = path.Skip(cycleIndex).Concat([next]).ToList();
                    var cycleKey = string.Join("→", cycleNodes.Select(ShortName));

                    // Report on each node in the cycle that we have symbol info for
                    foreach (var node in cycleNodes.Skip(0).Take(cycleNodes.Count - 1))
                    {
                        if (!reported.Add(node))
                            continue;

                        if (!symbolMap.TryGetValue(node, out var info))
                            continue;

                        foreach (var location in info.Locations)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                MafDiagnosticDescriptors.CyclicHandoffChain,
                                location,
                                info.Symbol.Name,
                                cycleKey));
                        }
                    }
                }
                else if (!visited.Contains(next))
                {
                    DetectCycle(next, adjacency, visited, path, reported, context, symbolMap);
                }
            }
        }

        path.RemoveAt(path.Count - 1);
        visited.Add(current);
    }

    private static string ShortName(string fqn)
    {
        // Strip "global::" prefix and return just the last segment for readability
        var clean = fqn.StartsWith("global::") ? fqn.Substring(8) : fqn;
        var dot = clean.LastIndexOf('.');
        return dot >= 0 ? clean.Substring(dot + 1) : clean;
    }
}
