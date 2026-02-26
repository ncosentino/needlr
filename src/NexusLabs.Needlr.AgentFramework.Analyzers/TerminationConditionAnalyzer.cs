using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that validates termination condition declarations on agent classes.
/// </summary>
/// <remarks>
/// <b>NDLRMAF009</b> (Warning): <c>[WorkflowRunTerminationCondition]</c> is declared on a class
/// that is not also decorated with <c>[NeedlrAiAgent]</c>.<br/>
/// <b>NDLRMAF010</b> (Error): The <c>conditionType</c> passed to
/// <c>[WorkflowRunTerminationCondition]</c> or <c>[AgentTerminationCondition]</c> does not
/// implement <c>IWorkflowTerminationCondition</c>.<br/>
/// <b>NDLRMAF011</b> (Info): <c>[WorkflowRunTerminationCondition]</c> is declared on a
/// <c>[AgentGroupChatMember]</c>; prefer <c>[AgentTerminationCondition]</c> for group chats.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TerminationConditionAnalyzer : DiagnosticAnalyzer
{
    private const string NeedlrAiAgentAttributeName = "NexusLabs.Needlr.AgentFramework.NeedlrAiAgentAttribute";
    private const string AgentGroupChatMemberAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGroupChatMemberAttribute";
    private const string WorkflowRunTerminationConditionAttributeName = "NexusLabs.Needlr.AgentFramework.WorkflowRunTerminationConditionAttribute";
    private const string AgentTerminationConditionAttributeName = "NexusLabs.Needlr.AgentFramework.AgentTerminationConditionAttribute";
    private const string IWorkflowTerminationConditionName = "NexusLabs.Needlr.AgentFramework.IWorkflowTerminationCondition";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            MafDiagnosticDescriptors.WorkflowRunTerminationConditionOnNonAgent,
            MafDiagnosticDescriptors.TerminationConditionTypeInvalid,
            MafDiagnosticDescriptors.PreferAgentTerminationConditionForGroupChat);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        bool isAgent = false;
        bool isGroupChatMember = false;
        var workflowRunConditionAttrs = new List<AttributeData>();
        var agentTerminationConditionAttrs = new List<AttributeData>();

        foreach (var attr in typeSymbol.GetAttributes())
        {
            var attrName = attr.AttributeClass?.ToDisplayString();

            if (attrName == NeedlrAiAgentAttributeName)
                isAgent = true;
            else if (attrName == AgentGroupChatMemberAttributeName)
                isGroupChatMember = true;
            else if (attrName == WorkflowRunTerminationConditionAttributeName)
                workflowRunConditionAttrs.Add(attr);
            else if (attrName == AgentTerminationConditionAttributeName)
                agentTerminationConditionAttrs.Add(attr);
        }

        if (workflowRunConditionAttrs.Count == 0 && agentTerminationConditionAttrs.Count == 0)
            return;

        var terminationInterface = context.Compilation.GetTypeByMetadataName(IWorkflowTerminationConditionName);

        foreach (var attr in workflowRunConditionAttrs)
        {
            var attrLocation = GetAttributeLocation(attr, typeSymbol);

            // NDLRMAF009: [WorkflowRunTerminationCondition] on a non-agent class
            if (!isAgent)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MafDiagnosticDescriptors.WorkflowRunTerminationConditionOnNonAgent,
                    attrLocation,
                    typeSymbol.Name));
            }

            // NDLRMAF010: conditionType doesn't implement IWorkflowTerminationCondition
            if (TryGetConditionType(attr, out var conditionType) && terminationInterface is not null)
            {
                if (!ImplementsInterface(conditionType!, terminationInterface))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MafDiagnosticDescriptors.TerminationConditionTypeInvalid,
                        attrLocation,
                        conditionType!.Name,
                        typeSymbol.Name));
                }
            }

            // NDLRMAF011: [WorkflowRunTerminationCondition] on [AgentGroupChatMember]
            if (isGroupChatMember)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MafDiagnosticDescriptors.PreferAgentTerminationConditionForGroupChat,
                    attrLocation,
                    typeSymbol.Name));
            }
        }

        foreach (var attr in agentTerminationConditionAttrs)
        {
            // NDLRMAF010: conditionType doesn't implement IWorkflowTerminationCondition
            if (TryGetConditionType(attr, out var conditionType) && terminationInterface is not null)
            {
                if (!ImplementsInterface(conditionType!, terminationInterface))
                {
                    var attrLocation = GetAttributeLocation(attr, typeSymbol);
                    context.ReportDiagnostic(Diagnostic.Create(
                        MafDiagnosticDescriptors.TerminationConditionTypeInvalid,
                        attrLocation,
                        conditionType!.Name,
                        typeSymbol.Name));
                }
            }
        }
    }

    private static bool TryGetConditionType(AttributeData attr, out INamedTypeSymbol? conditionType)
    {
        conditionType = null;

        if (attr.ConstructorArguments.Length >= 1
            && attr.ConstructorArguments[0].Kind == TypedConstantKind.Type
            && attr.ConstructorArguments[0].Value is INamedTypeSymbol namedType)
        {
            conditionType = namedType;
            return true;
        }

        return false;
    }

    private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol interfaceSymbol)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, interfaceSymbol))
                return true;
        }

        return false;
    }

    private static Location GetAttributeLocation(AttributeData attr, INamedTypeSymbol fallback)
    {
        return attr.ApplicationSyntaxReference?.SyntaxTree is { } tree
            ? Location.Create(tree, attr.ApplicationSyntaxReference.Span)
            : fallback.Locations[0];
    }
}
