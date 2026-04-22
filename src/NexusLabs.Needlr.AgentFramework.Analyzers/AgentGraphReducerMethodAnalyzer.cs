using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Validates that <c>ReducerMethod</c> on <c>[AgentGraphReducer]</c> references
/// a valid static method on the decorated class with signature
/// <c>static string MethodName(IReadOnlyList&lt;string&gt;)</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentGraphReducerMethodAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.GraphReducerMethodInvalid);

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
            if (attr.AttributeClass?.Name != "AgentGraphReducerAttribute" ||
                attr.AttributeClass.ContainingNamespace?.ToDisplayString() != "NexusLabs.Needlr.AgentFramework")
            {
                continue;
            }

            string? reducerMethodName = null;
            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "ReducerMethod" && named.Value.Value is string val)
                {
                    reducerMethodName = val;
                }
            }

            if (string.IsNullOrWhiteSpace(reducerMethodName))
            {
                continue;
            }

            var isValid = false;
            foreach (var member in typeSymbol.GetMembers(reducerMethodName!))
            {
                if (member is IMethodSymbol method &&
                    method.IsStatic &&
                    method.ReturnType.SpecialType == SpecialType.System_String &&
                    method.Parameters.Length == 1 &&
                    IsReadOnlyListOfString(method.Parameters[0].Type))
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
                    MafDiagnosticDescriptors.GraphReducerMethodInvalid,
                    location,
                    reducerMethodName,
                    typeSymbol.Name));
            }
        }
    }

    private static bool IsReadOnlyListOfString(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType || !namedType.IsGenericType)
        {
            return false;
        }

        var constructedFrom = namedType.ConstructedFrom;
        if (constructedFrom.Name != "IReadOnlyList" || constructedFrom.Arity != 1)
        {
            return false;
        }

        return namedType.TypeArguments[0].SpecialType == SpecialType.System_String;
    }
}
