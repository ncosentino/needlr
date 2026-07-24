// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Analyzer for <c>[HttpClientOptions]</c> usage. Enforces the contracts the generator
/// relies on and reports six diagnostics:
/// <list type="bullet">
/// <item><description>NDLRHTTP001 — target must implement <c>INamedHttpClientOptions</c></description></item>
/// <item><description>NDLRHTTP002 — attribute <c>Name</c> and <c>ClientName</c> property disagree</description></item>
/// <item><description>NDLRHTTP003 — <c>ClientName</c> property body is not a literal expression</description></item>
/// <item><description>NDLRHTTP004 — resolved name is empty</description></item>
/// <item><description>NDLRHTTP005 — duplicate client name across types in the compilation</description></item>
/// <item><description>NDLRHTTP006 — <c>ClientName</c> property has the wrong shape</description></item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HttpClientOptionsAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.HttpClientMustImplementMarker,
            DiagnosticDescriptors.HttpClientNameSourceConflict,
            DiagnosticDescriptors.HttpClientNamePropertyNotLiteral,
            DiagnosticDescriptors.HttpClientNameEmpty,
            DiagnosticDescriptors.HttpClientNameCollision,
            DiagnosticDescriptors.HttpClientNamePropertyWrongShape);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Collision detection needs the full compilation — use a compilation start action so
        // we can collect per-type resolved names from concurrent symbol actions and then
        // emit collision diagnostics in a compilation-end action.
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var nameToTypes = new ConcurrentDictionary<string, List<(INamedTypeSymbol Type, Location Location)>>();

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, nameToTypes),
                SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var kvp in nameToTypes)
                {
                    var participants = kvp.Value
                        .OrderBy(
                            participant =>
                                participant.Location.SourceTree?.FilePath ??
                                string.Empty,
                            System.StringComparer.Ordinal)
                        .ThenBy(
                            participant =>
                                participant.Location.SourceSpan.Start)
                        .ThenBy(
                            participant =>
                                participant.Type.ToDisplayString(),
                            System.StringComparer.Ordinal)
                        .ToArray();
                    if (participants.Length < 2)
                        continue;

                    // Report the collision on every participant after the first, pointing at
                    // the prior participant so both ends of the clash surface in the IDE.
                    var first = participants[0];
                    for (var i = 1; i < participants.Length; i++)
                    {
                        var dup = participants[i];
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.HttpClientNameCollision,
                            dup.Location,
                            dup.Type.Name,
                            kvp.Key,
                            first.Type.Name));
                    }
                }
            });
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        ConcurrentDictionary<string, List<(INamedTypeSymbol Type, Location Location)>> nameToTypes)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;
        var attrInfo = HttpClientOptionsAttributeHelper.GetHttpClientOptionsAttribute(typeSymbol);
        if (!attrInfo.HasValue)
            return;

        var typeLocation = typeSymbol.Locations.Length > 0 ? typeSymbol.Locations[0] : Location.None;
        var reportLocation = attrInfo.Value.AttributeLocation ?? typeLocation;

        // NDLRHTTP001: must implement INamedHttpClientOptions
        if (!HttpClientOptionsAttributeHelper.ImplementsNamedHttpClientOptions(typeSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.HttpClientMustImplementMarker,
                reportLocation,
                typeSymbol.Name));
            // Keep analyzing — the other diagnostics are still meaningful.
        }

        // NDLRHTTP006: ClientName property shape check — runs before the literal extraction so
        // a wrong-shape property doesn't silently fall through to type-name inference.
        var clientNameSymbol = HttpClientOptionsAttributeHelper.GetClientNamePropertySymbol(typeSymbol);
        if (clientNameSymbol is not null)
        {
            var isValidShape =
                clientNameSymbol.Type.SpecialType == SpecialType.System_String &&
                !clientNameSymbol.IsStatic &&
                clientNameSymbol.GetMethod is not null;

            if (!isValidShape)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.HttpClientNamePropertyWrongShape,
                    clientNameSymbol.Locations.Length > 0 ? clientNameSymbol.Locations[0] : reportLocation,
                    typeSymbol.Name));
            }
        }

        // Now resolve the name and check conflict / literal / empty rules.
        var propResult = HttpClientOptionsAttributeHelper.TryGetClientNameProperty(typeSymbol, out var literalValue);
        var attributeName = attrInfo.Value.Name;

        // NDLRHTTP003: non-literal ClientName without an attribute Name fallback
        if (propResult == ClientNamePropertyResult.NonLiteral && string.IsNullOrWhiteSpace(attributeName))
        {
            // Only report if the property shape was otherwise valid — NDLRHTTP006 already fired
            // for shape issues, and piling NDLRHTTP003 on top would be noise.
            if (clientNameSymbol is not null &&
                clientNameSymbol.Type.SpecialType == SpecialType.System_String &&
                !clientNameSymbol.IsStatic &&
                clientNameSymbol.GetMethod is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.HttpClientNamePropertyNotLiteral,
                    clientNameSymbol.Locations.Length > 0 ? clientNameSymbol.Locations[0] : reportLocation,
                    typeSymbol.Name));
            }
        }

        // NDLRHTTP002: attribute Name and literal ClientName property disagree
        if (!string.IsNullOrWhiteSpace(attributeName) &&
            propResult == ClientNamePropertyResult.Literal &&
            !string.IsNullOrWhiteSpace(literalValue) &&
            !string.Equals(attributeName, literalValue, System.StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.HttpClientNameSourceConflict,
                reportLocation,
                typeSymbol.Name,
                attributeName!,
                literalValue!));
        }

        var propertyName = propResult == ClientNamePropertyResult.Literal
            ? literalValue
            : null;
        if (!HttpClientOptionsAttributeHelper.TryResolveClientName(
            typeSymbol,
            attrInfo.Value,
            propertyName,
            out var effectiveName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.HttpClientNameEmpty,
                reportLocation,
                typeSymbol.Name));
            return;
        }

        // Record for NDLRHTTP005 collision detection.
        nameToTypes.AddOrUpdate(
            effectiveName,
            _ => new List<(INamedTypeSymbol, Location)> { (typeSymbol, reportLocation) },
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add((typeSymbol, reportLocation));
                    return existing;
                }
            });
    }
}
