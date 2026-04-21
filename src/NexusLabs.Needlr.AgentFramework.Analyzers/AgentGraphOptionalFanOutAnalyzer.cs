using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that detects fan-out nodes where all outgoing edges are optional.
/// </summary>
/// <remarks>
/// <b>NDLRMAF024</b> (Warning): All outgoing edges from a fan-out node have
/// <c>IsRequired = false</c>. If all optional branches fail, the graph produces empty results.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentGraphOptionalFanOutAnalyzer : DiagnosticAnalyzer
{
    private const string AgentGraphEdgeAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGraphEdgeAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.GraphAllEdgesOptional);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // graphName → { sourceFqn → list of (isRequired, symbol, location) }
            var edgeData = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentBag<(bool IsRequired, INamedTypeSymbol Symbol, Location Location)>>>(
                StringComparer.Ordinal);

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;
                var fqn = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                foreach (var attr in typeSymbol.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() != AgentGraphEdgeAttributeName)
                        continue;

                    if (attr.ConstructorArguments.Length < 2)
                        continue;

                    if (attr.ConstructorArguments[0].Value is not string graphName)
                        continue;

                    // IsRequired defaults to true
                    var isRequired = true;
                    foreach (var namedArg in attr.NamedArguments)
                    {
                        if (namedArg.Key == "IsRequired" && namedArg.Value.Value is bool val)
                        {
                            isRequired = val;
                        }
                    }

                    var location = typeSymbol.Locations[0];

                    var perGraph = edgeData.GetOrAdd(graphName, _ => new ConcurrentDictionary<string, ConcurrentBag<(bool, INamedTypeSymbol, Location)>>(StringComparer.Ordinal));
                    perGraph.GetOrAdd(fqn, _ => new ConcurrentBag<(bool, INamedTypeSymbol, Location)>())
                        .Add((isRequired, typeSymbol, location));
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var graphKvp in edgeData)
                {
                    var graphName = graphKvp.Key;

                    foreach (var sourceKvp in graphKvp.Value)
                    {
                        var edges = sourceKvp.Value.ToList();

                        // Only flag fan-out (2+ outgoing edges) where all are optional
                        if (edges.Count < 2)
                            continue;

                        if (edges.Any(e => e.IsRequired))
                            continue;

                        // All edges are optional
                        var symbol = edges[0].Symbol;
                        var location = edges[0].Location;

                        endContext.ReportDiagnostic(Diagnostic.Create(
                            MafDiagnosticDescriptors.GraphAllEdgesOptional,
                            location,
                            edges.Count,
                            symbol.Name,
                            graphName));
                    }
                }
            });
        });
    }
}
