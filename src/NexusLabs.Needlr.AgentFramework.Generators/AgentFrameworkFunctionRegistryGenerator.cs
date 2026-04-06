// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NexusLabs.Needlr.AgentFramework.Generators;

/// <summary>
/// Source generator for Microsoft Agent Framework functions.
/// Discovers classes with [AgentFunction] methods and generates a compile-time type registry.
/// Also discovers classes with [AgentFunctionGroup] attributes and generates a group registry.
/// Also discovers classes with [NeedlrAiAgent] attributes and generates an agent registry.
/// Always emits a [ModuleInitializer] that auto-registers all discovered types with
/// AgentFrameworkGeneratedBootstrap on assembly load.
/// </summary>
[Generator]
public class AgentFrameworkFunctionRegistryGenerator : IIncrementalGenerator
{
    private const string NeedlrAiAgentAttributeName = "NexusLabs.Needlr.AgentFramework.NeedlrAiAgentAttribute";
    private const string AgentHandoffsToAttributeName = "NexusLabs.Needlr.AgentFramework.AgentHandoffsToAttribute";
    private const string AgentGroupChatMemberAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGroupChatMemberAttribute";
    private const string AgentSequenceMemberAttributeName = "NexusLabs.Needlr.AgentFramework.AgentSequenceMemberAttribute";
    private const string WorkflowRunTerminationConditionAttributeName = "NexusLabs.Needlr.AgentFramework.WorkflowRunTerminationConditionAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // [AgentFunction] method-bearing classes → AgentFrameworkFunctionRegistry
        var functionClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => AgentDiscoveryHelper.GetAgentFunctionTypeInfo(ctx, ct))
            .Where(static m => m is not null);

        // [AgentFunctionGroup] class-level annotations → AgentFrameworkFunctionGroupRegistry
        var groupClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => AgentDiscoveryHelper.GetAgentFunctionGroupEntries(ctx, ct))
            .Where(static arr => arr.Length > 0);

        // [NeedlrAiAgent] declared agent types → AgentRegistry + partial companions
        var agentClasses = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                NeedlrAiAgentAttributeName,
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => AgentDiscoveryHelper.GetNeedlrAiAgentTypeInfo(ctx, ct))
            .Where(static m => m is not null);

        // [AgentHandoffsTo] annotations → handoff topology registry
        var handoffEntries = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AgentHandoffsToAttributeName,
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => AgentDiscoveryHelper.GetHandoffEntries(ctx, ct))
            .Where(static arr => arr.Length > 0);

        // [AgentGroupChatMember] annotations → group chat registry
        var groupChatEntries = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AgentGroupChatMemberAttributeName,
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => AgentDiscoveryHelper.GetGroupChatEntries(ctx, ct))
            .Where(static arr => arr.Length > 0);

        // [AgentSequenceMember] annotations → sequential pipeline registry
        var sequenceEntries = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AgentSequenceMemberAttributeName,
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => AgentDiscoveryHelper.GetSequenceEntries(ctx, ct))
            .Where(static arr => arr.Length > 0);

        // [WorkflowRunTerminationCondition] → termination conditions per agent
        var terminationConditionEntries = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                WorkflowRunTerminationConditionAttributeName,
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => AgentDiscoveryHelper.GetTerminationConditionEntries(ctx, ct))
            .Where(static arr => arr.Length > 0);

        // Unified output: all seven pipelines combined with compilation metadata and build config.
        // Always emits all registries + [ModuleInitializer] bootstrap, even when empty.
        var combined = functionClasses.Collect()
            .Combine(groupClasses.Collect())
            .Combine(agentClasses.Collect())
            .Combine(handoffEntries.Collect())
            .Combine(groupChatEntries.Collect())
            .Combine(sequenceEntries.Collect())
            .Combine(terminationConditionEntries.Collect())
            .Combine(context.CompilationProvider)
            .Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(combined,
            static (spc, data) =>
            {
                var ((((((((functionData, groupData), agentData), handoffData), groupChatData), sequenceData), terminationData), compilation), configOptions) = data;
                ExecuteAll(functionData, groupData, agentData, handoffData, groupChatData, sequenceData, terminationData, compilation, configOptions, spc);
            });
    }

    private static void ExecuteAll(
        ImmutableArray<AgentFunctionTypeInfo?> functionData,
        ImmutableArray<ImmutableArray<AgentFunctionGroupEntry>> groupData,
        ImmutableArray<NeedlrAiAgentTypeInfo?> agentData,
        ImmutableArray<ImmutableArray<HandoffEntry>> handoffData,
        ImmutableArray<ImmutableArray<GroupChatEntry>> groupChatData,
        ImmutableArray<ImmutableArray<SequenceEntry>> sequenceData,
        ImmutableArray<ImmutableArray<TerminationConditionEntry>> terminationData,
        Compilation compilation,
        Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider configOptions,
        SourceProductionContext spc)
    {
        var assemblyName = compilation.AssemblyName ?? "UnknownAssembly";
        var safeAssemblyName = AgentDiscoveryHelper.SanitizeIdentifier(assemblyName);

        var validFunctionTypes = functionData
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .ToList();

        var allGroupEntries = groupData.SelectMany(a => a).ToList();
        var groupedByName = allGroupEntries
            .GroupBy(e => e.GroupName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.TypeName).Distinct().ToList());

        var validAgentTypes = agentData
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .ToList();

        var allHandoffEntries = handoffData.SelectMany(a => a).ToList();
        var handoffByInitialAgent = allHandoffEntries
            .GroupBy(e => (e.InitialAgentTypeName, e.InitialAgentClassName))
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => (e.TargetAgentTypeName, e.HandoffReason)).ToList());

        var allGroupChatEntries = groupChatData.SelectMany(a => a).ToList();
        var groupChatByGroupName = allGroupChatEntries
            .GroupBy(e => e.GroupName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.AgentTypeName).Distinct().ToList());

        var allSequenceEntries = sequenceData.SelectMany(a => a).ToList();
        var sequenceByPipelineName = allSequenceEntries
            .GroupBy(e => e.PipelineName)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(e => e.Order).Select(e => e.AgentTypeName).ToList());

        var conditionsByAgentTypeName = terminationData
            .SelectMany(a => a)
            .GroupBy(e => e.AgentTypeName)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Always emit all registries (may be empty) and the bootstrap
        spc.AddSource("AgentFrameworkFunctions.g.cs",
            SourceText.From(RegistryCodeGenerator.GenerateRegistrySource(validFunctionTypes, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentFrameworkFunctionGroups.g.cs",
            SourceText.From(RegistryCodeGenerator.GenerateGroupRegistrySource(groupedByName, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentRegistry.g.cs",
            SourceText.From(RegistryCodeGenerator.GenerateAgentRegistrySource(validAgentTypes, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentHandoffTopologyRegistry.g.cs",
            SourceText.From(RegistryCodeGenerator.GenerateHandoffTopologyRegistrySource(handoffByInitialAgent, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentGroupChatRegistry.g.cs",
            SourceText.From(RegistryCodeGenerator.GenerateGroupChatRegistrySource(groupChatByGroupName, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentSequentialTopologyRegistry.g.cs",
            SourceText.From(RegistryCodeGenerator.GenerateSequentialTopologyRegistrySource(sequenceByPipelineName, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("NeedlrAgentFrameworkBootstrap.g.cs",
            SourceText.From(BootstrapCodeGenerator.GenerateBootstrapSource(safeAssemblyName), Encoding.UTF8));

        spc.AddSource("WorkflowFactoryExtensions.g.cs",
            SourceText.From(ExtensionsCodeGenerator.GenerateWorkflowFactoryExtensionsSource(
                handoffByInitialAgent, groupChatByGroupName, sequenceByPipelineName,
                conditionsByAgentTypeName, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentFactoryExtensions.g.cs",
            SourceText.From(ExtensionsCodeGenerator.GenerateAgentFactoryExtensionsSource(validAgentTypes, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentTopologyConstants.g.cs",
            SourceText.From(ExtensionsCodeGenerator.GenerateAgentTopologyConstantsSource(validAgentTypes, allGroupEntries, sequenceByPipelineName, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentFrameworkSyringeExtensions.g.cs",
            SourceText.From(ExtensionsCodeGenerator.GenerateSyringeExtensionsSource(allGroupEntries, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("GeneratedAIFunctionProvider.g.cs",
            SourceText.From(AIFunctionProviderCodeGenerator.GenerateAIFunctionProviderSource(validFunctionTypes, safeAssemblyName), Encoding.UTF8));

        configOptions.GlobalOptions.TryGetValue("build_property.NeedlrDiagnostics", out var diagValue);
        if (string.Equals(diagValue, "true", StringComparison.OrdinalIgnoreCase))
        {
            var mermaid = TopologyGraphCodeGenerator.GenerateMermaidDiagram(handoffByInitialAgent, groupChatByGroupName, sequenceByPipelineName);

            spc.AddSource("AgentTopologyGraph.g.cs",
                SourceText.From(TopologyGraphCodeGenerator.GenerateTopologyGraphSource(mermaid, safeAssemblyName), Encoding.UTF8));
        }

        // Partial companions for [NeedlrAiAgent] classes declared as partial
        foreach (var agentType in validAgentTypes.Where(a => a.IsPartial))
        {
            var safeTypeName = agentType.TypeName
                .Replace("global::", "")
                .Replace(".", "_")
                .Replace("<", "_")
                .Replace(">", "_");

            spc.AddSource($"{safeTypeName}.NeedlrAiAgent.g.cs",
                SourceText.From(BootstrapCodeGenerator.GeneratePartialCompanionSource(agentType, groupedByName), Encoding.UTF8));
        }
    }
}
