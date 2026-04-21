using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that validates entry point declarations for agent graphs.
/// </summary>
/// <remarks>
/// <para>
/// <b>NDLRMAF017</b> (Error): A named graph has edges but no <c>[AgentGraphEntry]</c>.
/// </para>
/// <para>
/// <b>NDLRMAF018</b> (Error): A named graph has multiple <c>[AgentGraphEntry]</c> declarations.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentGraphEntryPointAnalyzer : DiagnosticAnalyzer
{
    private const string AgentGraphEdgeAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGraphEdgeAttribute";
    private const string AgentGraphEntryAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGraphEntryAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            MafDiagnosticDescriptors.GraphNoEntryPoint,
            MafDiagnosticDescriptors.GraphMultipleEntryPoints);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // graphName → list of (typeName, location)
            var entryPoints = new ConcurrentDictionary<string, ConcurrentBag<(string TypeName, Location Location)>>(StringComparer.Ordinal);
            // graphName → set of edge attribute locations (for reporting NDLRMAF017)
            var edgeGraphNames = new ConcurrentDictionary<string, ConcurrentBag<Location>>(StringComparer.Ordinal);

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;

                foreach (var attr in typeSymbol.GetAttributes())
                {
                    var attrName = attr.AttributeClass?.ToDisplayString();

                    if (attrName == AgentGraphEntryAttributeName)
                    {
                        if (attr.ConstructorArguments.Length < 1)
                            continue;

                        if (attr.ConstructorArguments[0].Value is not string graphName)
                            continue;

                        var location = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                            ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                            : typeSymbol.Locations[0];

                        entryPoints.GetOrAdd(graphName, _ => new ConcurrentBag<(string, Location)>())
                            .Add((typeSymbol.Name, location));
                    }

                    if (attrName == AgentGraphEdgeAttributeName)
                    {
                        if (attr.ConstructorArguments.Length < 1)
                            continue;

                        if (attr.ConstructorArguments[0].Value is not string graphName)
                            continue;

                        var location = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                            ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                            : typeSymbol.Locations[0];

                        edgeGraphNames.GetOrAdd(graphName, _ => new ConcurrentBag<Location>())
                            .Add(location);
                    }
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                // Collect all graph names (union of entry and edge declarations)
                var allGraphNames = new HashSet<string>(edgeGraphNames.Keys, StringComparer.Ordinal);

                foreach (var graphName in allGraphNames)
                {
                    if (!entryPoints.TryGetValue(graphName, out var entries) || entries.Count == 0)
                    {
                        // NDLRMAF017: edges exist but no entry point
                        if (edgeGraphNames.TryGetValue(graphName, out var edgeLocations))
                        {
                            var firstEdgeLocation = edgeLocations.First();
                            endContext.ReportDiagnostic(Diagnostic.Create(
                                MafDiagnosticDescriptors.GraphNoEntryPoint,
                                firstEdgeLocation,
                                graphName));
                        }
                    }
                    else if (entries.Count > 1)
                    {
                        // NDLRMAF018: multiple entry points
                        var entryList = entries.ToList();
                        var typeNames = string.Join(", ", entryList.Select(e => e.TypeName));
                        foreach (var entry in entryList)
                        {
                            endContext.ReportDiagnostic(Diagnostic.Create(
                                MafDiagnosticDescriptors.GraphMultipleEntryPoints,
                                entry.Location,
                                graphName,
                                typeNames));
                        }
                    }
                }

                // Also check entry-only graphs (entry declared but no edges) for multiple entries
                foreach (var graphName in entryPoints.Keys)
                {
                    if (allGraphNames.Contains(graphName))
                        continue;

                    if (entryPoints.TryGetValue(graphName, out var entries) && entries.Count > 1)
                    {
                        var entryList = entries.ToList();
                        var typeNames = string.Join(", ", entryList.Select(e => e.TypeName));
                        foreach (var entry in entryList)
                        {
                            endContext.ReportDiagnostic(Diagnostic.Create(
                                MafDiagnosticDescriptors.GraphMultipleEntryPoints,
                                entry.Location,
                                graphName,
                                typeNames));
                        }
                    }
                }
            });
        });
    }
}
