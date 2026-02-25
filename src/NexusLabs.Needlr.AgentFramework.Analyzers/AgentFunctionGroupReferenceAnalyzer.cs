using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that validates <c>FunctionGroups</c> references in <c>[NeedlrAiAgent]</c> declarations.
/// </summary>
/// <remarks>
/// <b>NDLRMAF005</b> (Warning): An agent declares a <c>FunctionGroups</c> entry whose name does not
/// match any class decorated with <c>[AgentFunctionGroup]</c> in this compilation. The agent will
/// silently receive zero tools from that group at runtime.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentFunctionGroupReferenceAnalyzer : DiagnosticAnalyzer
{
    private const string NeedlrAiAgentAttributeName = "NexusLabs.Needlr.AgentFramework.NeedlrAiAgentAttribute";
    private const string AgentFunctionGroupAttributeName = "NexusLabs.Needlr.AgentFramework.AgentFunctionGroupAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.UnresolvedFunctionGroupReference);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var knownGroups = new ConcurrentBag<string>();
            var references = new ConcurrentBag<(string AgentName, string GroupName, Location Location)>();

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;

                foreach (var attr in typeSymbol.GetAttributes())
                {
                    var attrName = attr.AttributeClass?.ToDisplayString();

                    if (attrName == AgentFunctionGroupAttributeName
                        && attr.ConstructorArguments.Length >= 1
                        && attr.ConstructorArguments[0].Value is string groupName
                        && !string.IsNullOrWhiteSpace(groupName))
                    {
                        knownGroups.Add(groupName);
                    }

                    if (attrName == NeedlrAiAgentAttributeName)
                    {
                        var functionGroupsArg = attr.NamedArguments.FirstOrDefault(a => a.Key == "FunctionGroups");
                        if (functionGroupsArg.Key is null)
                            continue;

                        var arrayConstant = functionGroupsArg.Value;
                        if (arrayConstant.Kind != TypedConstantKind.Array)
                            continue;

                        var attrLocation = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                            ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                            : typeSymbol.Locations[0];

                        foreach (var item in arrayConstant.Values)
                        {
                            if (item.Value is string name && !string.IsNullOrWhiteSpace(name))
                                references.Add((typeSymbol.Name, name, attrLocation));
                        }
                    }
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                var knownGroupSet = new HashSet<string>(knownGroups, StringComparer.Ordinal);

                foreach (var (agentName, groupName, location) in references)
                {
                    if (!knownGroupSet.Contains(groupName))
                    {
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            MafDiagnosticDescriptors.UnresolvedFunctionGroupReference,
                            location,
                            agentName,
                            groupName));
                    }
                }
            });
        });
    }
}
