using System.Collections.Concurrent;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that validates <c>[AgentGroupChatMember]</c> group declarations.
/// </summary>
/// <remarks>
/// <b>NDLRMAF002</b> (Error): A named group chat has fewer than two members in this compilation.
/// <c>IWorkflowFactory.CreateGroupChatWorkflow</c> throws at runtime when this condition is met.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentGroupChatSingletonAnalyzer : DiagnosticAnalyzer
{
    private const string AgentGroupChatMemberAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGroupChatMemberAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.GroupChatTooFewMembers);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // group name → list of (type symbol, attribute location)
            var groupMembers = new ConcurrentDictionary<string, ConcurrentBag<(INamedTypeSymbol Type, Location Location)>>(
                StringComparer.Ordinal);

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;

                foreach (var attr in typeSymbol.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() != AgentGroupChatMemberAttributeName)
                        continue;

                    if (attr.ConstructorArguments.Length < 1)
                        continue;

                    var groupName = attr.ConstructorArguments[0].Value as string;
                    if (string.IsNullOrWhiteSpace(groupName))
                        continue;

                    var attrLocation = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                        ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                        : typeSymbol.Locations[0];

                    groupMembers.GetOrAdd(groupName!, _ => new ConcurrentBag<(INamedTypeSymbol, Location)>())
                        .Add((typeSymbol, attrLocation));
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var kvp in groupMembers)
                {
                    var members = kvp.Value.ToList();
                    if (members.Count >= 2)
                        continue;

                    // Report on every contributing class so the squiggle appears where the attribute is
                    foreach (var (_, location) in members)
                    {
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            MafDiagnosticDescriptors.GroupChatTooFewMembers,
                            location,
                            kvp.Key,
                            members.Count));
                    }
                }
            });
        });
    }
}
