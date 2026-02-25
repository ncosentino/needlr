using System.Collections.Concurrent;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that detects agent types that participate in no topology declaration.
/// </summary>
/// <remarks>
/// <b>NDLRMAF008</b> (Info): A class decorated with <c>[NeedlrAiAgent]</c> is not referenced in any
/// topology attribute (<c>[AgentHandoffsTo]</c>, <c>[AgentGroupChatMember]</c>, or
/// <c>[AgentSequenceMember]</c>). This may indicate an orphaned or work-in-progress agent.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentOrphanAnalyzer : DiagnosticAnalyzer
{
    private const string NeedlrAiAgentAttributeName = "NexusLabs.Needlr.AgentFramework.NeedlrAiAgentAttribute";
    private const string AgentHandoffsToAttributeName = "NexusLabs.Needlr.AgentFramework.AgentHandoffsToAttribute";
    private const string AgentGroupChatMemberAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGroupChatMemberAttribute";
    private const string AgentSequenceMemberAttributeName = "NexusLabs.Needlr.AgentFramework.AgentSequenceMemberAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.OrphanAgent);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var agents = new ConcurrentDictionary<string, (INamedTypeSymbol Symbol, Location Location)>(StringComparer.Ordinal);
            var topologyParticipants = new ConcurrentBag<string>();

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;
                var fqn = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                Location? agentAttrLocation = null;
                bool inTopology = false;

                foreach (var attr in typeSymbol.GetAttributes())
                {
                    var attrName = attr.AttributeClass?.ToDisplayString();

                    if (attrName == NeedlrAiAgentAttributeName)
                    {
                        agentAttrLocation = attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
                            ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
                            : typeSymbol.Locations[0];
                    }

                    if (attrName == AgentGroupChatMemberAttributeName || attrName == AgentSequenceMemberAttributeName)
                        inTopology = true;

                    if (attrName == AgentHandoffsToAttributeName)
                    {
                        inTopology = true;

                        // The handoff target is also a topology participant
                        if (attr.ConstructorArguments.Length >= 1
                            && attr.ConstructorArguments[0].Kind == TypedConstantKind.Type
                            && attr.ConstructorArguments[0].Value is INamedTypeSymbol targetType)
                        {
                            topologyParticipants.Add(
                                targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                        }
                    }
                }

                if (agentAttrLocation is not null)
                    agents[fqn] = (typeSymbol, agentAttrLocation);

                if (inTopology)
                    topologyParticipants.Add(fqn);
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                var participantSet = new HashSet<string>(topologyParticipants, StringComparer.Ordinal);

                foreach (var kvp in agents)
                {
                    if (!participantSet.Contains(kvp.Key))
                    {
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            MafDiagnosticDescriptors.OrphanAgent,
                            kvp.Value.Location,
                            kvp.Value.Symbol.Name));
                    }
                }
            });
        });
    }
}
