using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Validates that <c>Condition</c> on <c>[AgentGraphEdge]</c> references a
/// valid static method on the decorated class with signature
/// <c>static bool MethodName(object?)</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentGraphConditionMethodAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.GraphConditionMethodInvalid);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "AgentGraphEdgeAttribute" ||
                attr.AttributeClass.ContainingNamespace?.ToDisplayString() != "NexusLabs.Needlr.AgentFramework")
            {
                continue;
            }

            string? conditionName = null;
            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "Condition" && named.Value.Value is string val)
                {
                    conditionName = val;
                }
            }

            if (string.IsNullOrWhiteSpace(conditionName))
            {
                continue;
            }

            var isValid = false;
            foreach (var member in typeSymbol.GetMembers(conditionName!))
            {
                if (member is IMethodSymbol method &&
                    method.IsStatic &&
                    method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                    method.Parameters.Length == 1)
                {
                    isValid = true;
                    break;
                }
            }

            if (!isValid)
            {
                var location = attr.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                    ?? typeSymbol.Locations.FirstOrDefault()
                    ?? Location.None;

                context.ReportDiagnostic(Diagnostic.Create(
                    MafDiagnosticDescriptors.GraphConditionMethodInvalid,
                    location,
                    conditionName,
                    typeSymbol.Name));
            }
        }
    }
}
