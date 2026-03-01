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
    private const string AgentFunctionAttributeName = "NexusLabs.Needlr.AgentFramework.AgentFunctionAttribute";
    private const string AgentFunctionGroupAttributeName = "NexusLabs.Needlr.AgentFramework.AgentFunctionGroupAttribute";
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
                transform: static (ctx, ct) => GetAgentFunctionTypeInfo(ctx, ct))
            .Where(static m => m is not null);

        // [AgentFunctionGroup] class-level annotations → AgentFrameworkFunctionGroupRegistry
        var groupClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetAgentFunctionGroupEntries(ctx, ct))
            .Where(static arr => arr.Length > 0);

        // [NeedlrAiAgent] declared agent types → AgentRegistry + partial companions
        var agentClasses = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                NeedlrAiAgentAttributeName,
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetNeedlrAiAgentTypeInfo(ctx, ct))
            .Where(static m => m is not null);

        // [AgentHandoffsTo] annotations → handoff topology registry
        var handoffEntries = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AgentHandoffsToAttributeName,
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetHandoffEntries(ctx, ct))
            .Where(static arr => arr.Length > 0);

        // [AgentGroupChatMember] annotations → group chat registry
        var groupChatEntries = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AgentGroupChatMemberAttributeName,
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetGroupChatEntries(ctx, ct))
            .Where(static arr => arr.Length > 0);

        // [AgentSequenceMember] annotations → sequential pipeline registry
        var sequenceEntries = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AgentSequenceMemberAttributeName,
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetSequenceEntries(ctx, ct))
            .Where(static arr => arr.Length > 0);

        // [WorkflowRunTerminationCondition] → termination conditions per agent
        var terminationConditionEntries = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                WorkflowRunTerminationConditionAttributeName,
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetTerminationConditionEntries(ctx, ct))
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

    private static AgentFunctionTypeInfo? GetAgentFunctionTypeInfo(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var typeSymbol = context.SemanticModel
            .GetDeclaredSymbol(classDeclaration, cancellationToken) as INamedTypeSymbol;

        if (typeSymbol is null)
            return null;

        return TryGetTypeInfo(typeSymbol);
    }

    private static AgentFunctionTypeInfo? TryGetTypeInfo(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeKind != TypeKind.Class)
            return null;

        if (!IsAccessibleFromGeneratedCode(typeSymbol))
            return null;

        if (!typeSymbol.IsStatic && typeSymbol.IsAbstract)
            return null;

        bool hasAgentFunction = false;
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            if (method.MethodKind != MethodKind.Ordinary)
                continue;

            if (method.DeclaredAccessibility != Accessibility.Public)
                continue;

            foreach (var attribute in method.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == AgentFunctionAttributeName)
                {
                    hasAgentFunction = true;
                    break;
                }
            }

            if (hasAgentFunction)
                break;
        }

        if (!hasAgentFunction)
            return null;

        var methodInfos = ImmutableArray.CreateBuilder<AgentFunctionMethodInfo>();
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            if (method.MethodKind != MethodKind.Ordinary)
                continue;

            if (method.DeclaredAccessibility != Accessibility.Public)
                continue;

            bool isAgentFunction = false;
            foreach (var attribute in method.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == AgentFunctionAttributeName)
                {
                    isAgentFunction = true;
                    break;
                }
            }

            if (!isAgentFunction)
                continue;

            var returnType = method.ReturnType;
            bool isVoid = returnType.SpecialType == SpecialType.System_Void;
            bool isTask = returnType is INamedTypeSymbol nt &&
                nt.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks" &&
                (nt.MetadataName == "Task" || nt.MetadataName == "ValueTask");
            bool isTaskOfT = returnType is INamedTypeSymbol nt2 &&
                nt2.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks" &&
                (nt2.MetadataName == "Task`1" || nt2.MetadataName == "ValueTask`1") &&
                nt2.TypeArguments.Length == 1;
            bool isAsync = isTask || isTaskOfT;
            bool isVoidLike = isVoid || isTask;
            string? returnValueTypeFQN = isVoidLike ? null : isTaskOfT
                ? GetFullyQualifiedName(((INamedTypeSymbol)returnType).TypeArguments[0])
                : GetFullyQualifiedName(returnType);

            string? methodDesc = GetDescriptionFromAttributes(method.GetAttributes());

            var parameters = ImmutableArray.CreateBuilder<AgentFunctionParameterInfo>();
            foreach (var param in method.Parameters)
            {
                bool isCancellationToken = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken";
                bool isNullable = param.NullableAnnotation == NullableAnnotation.Annotated ||
                    (param.Type is INamedTypeSymbol pnt && pnt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T);
                bool hasDefault = param.HasExplicitDefaultValue;
                string? paramDesc = GetDescriptionFromAttributes(param.GetAttributes());
                string jsonSchemaType = GetJsonSchemaType(param.Type, out string? itemJsonSchemaType);
                string typeFullName = GetFullyQualifiedName(param.Type);

                parameters.Add(new AgentFunctionParameterInfo(
                    param.Name, typeFullName, jsonSchemaType, itemJsonSchemaType,
                    isCancellationToken, isNullable, hasDefault, paramDesc));
            }

            methodInfos.Add(new AgentFunctionMethodInfo(
                method.Name, isAsync, isVoidLike, returnValueTypeFQN,
                parameters.ToImmutable(), methodDesc ?? ""));
        }

        return new AgentFunctionTypeInfo(
            GetFullyQualifiedName(typeSymbol),
            typeSymbol.ContainingAssembly?.Name ?? "Unknown",
            typeSymbol.IsStatic,
            methodInfos.ToImmutable());
    }

    private static ImmutableArray<AgentFunctionGroupEntry> GetAgentFunctionGroupEntries(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var typeSymbol = context.SemanticModel
            .GetDeclaredSymbol(classDeclaration, cancellationToken) as INamedTypeSymbol;

        if (typeSymbol is null || !IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<AgentFunctionGroupEntry>.Empty;

        var entries = ImmutableArray.CreateBuilder<AgentFunctionGroupEntry>();

        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != AgentFunctionGroupAttributeName)
                continue;

            if (attr.ConstructorArguments.Length != 1)
                continue;

            var groupName = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(groupName))
                continue;

            entries.Add(new AgentFunctionGroupEntry(GetFullyQualifiedName(typeSymbol), groupName!));
        }

        return entries.ToImmutable();
    }

    private static NeedlrAiAgentTypeInfo? GetNeedlrAiAgentTypeInfo(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !IsAccessibleFromGeneratedCode(typeSymbol))
            return null;

        var classDeclaration = context.TargetNode as ClassDeclarationSyntax;
        var isPartial = classDeclaration?.Modifiers.Any(m => m.ValueText == "partial") ?? false;

        var namespaceName = typeSymbol.ContainingNamespace?.IsGlobalNamespace == true
            ? null
            : typeSymbol.ContainingNamespace?.ToDisplayString();

        var functionGroupNames = ImmutableArray<string>.Empty;
        var explicitFunctionTypeFQNs = ImmutableArray<string>.Empty;
        var hasExplicitFunctionTypes = false;

        var agentAttr = context.Attributes.FirstOrDefault();
        if (agentAttr is not null)
        {
            var groupsArg = agentAttr.NamedArguments.FirstOrDefault(a => a.Key == "FunctionGroups");
            if (groupsArg.Key is not null && groupsArg.Value.Kind == TypedConstantKind.Array)
            {
                functionGroupNames = groupsArg.Value.Values
                    .Select(v => v.Value as string)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToImmutableArray();
            }

            var typesArg = agentAttr.NamedArguments.FirstOrDefault(a => a.Key == "FunctionTypes");
            if (typesArg.Key is not null && typesArg.Value.Kind == TypedConstantKind.Array)
            {
                hasExplicitFunctionTypes = true;
                explicitFunctionTypeFQNs = typesArg.Value.Values
                    .Where(v => v.Kind == TypedConstantKind.Type && v.Value is INamedTypeSymbol)
                    .Select(v => GetFullyQualifiedName((INamedTypeSymbol)v.Value!))
                    .ToImmutableArray();
            }
        }

        return new NeedlrAiAgentTypeInfo(
            GetFullyQualifiedName(typeSymbol),
            typeSymbol.Name,
            namespaceName,
            isPartial,
            functionGroupNames,
            explicitFunctionTypeFQNs,
            hasExplicitFunctionTypes);
    }

    private static ImmutableArray<HandoffEntry> GetHandoffEntries(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<HandoffEntry>.Empty;

        var initialTypeName = GetFullyQualifiedName(typeSymbol);
        var entries = ImmutableArray.CreateBuilder<HandoffEntry>();

        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length < 1)
                continue;

            var typeArg = attr.ConstructorArguments[0];
            if (typeArg.Kind != TypedConstantKind.Type || typeArg.Value is not INamedTypeSymbol targetTypeSymbol)
                continue;

            var targetTypeName = GetFullyQualifiedName(targetTypeSymbol);
            var reason = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1].Value as string : null;

            entries.Add(new HandoffEntry(initialTypeName, typeSymbol.Name, targetTypeName, reason));
        }

        return entries.ToImmutable();
    }

    private static ImmutableArray<GroupChatEntry> GetGroupChatEntries(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<GroupChatEntry>.Empty;

        var agentTypeName = GetFullyQualifiedName(typeSymbol);
        var entries = ImmutableArray.CreateBuilder<GroupChatEntry>();

        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length < 1)
                continue;

            var groupName = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(groupName))
                continue;

            entries.Add(new GroupChatEntry(agentTypeName, groupName!));
        }

        return entries.ToImmutable();
    }

    private static ImmutableArray<SequenceEntry> GetSequenceEntries(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<SequenceEntry>.Empty;

        var agentTypeName = GetFullyQualifiedName(typeSymbol);
        var entries = ImmutableArray.CreateBuilder<SequenceEntry>();

        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length < 2)
                continue;

            var pipelineName = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(pipelineName))
                continue;

            if (attr.ConstructorArguments[1].Value is not int order)
                continue;

            entries.Add(new SequenceEntry(agentTypeName, pipelineName!, order));
        }

        return entries.ToImmutable();
    }

    private static ImmutableArray<TerminationConditionEntry> GetTerminationConditionEntries(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<TerminationConditionEntry>.Empty;

        var agentTypeName = GetFullyQualifiedName(typeSymbol);
        var entries = ImmutableArray.CreateBuilder<TerminationConditionEntry>();

        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length < 1)
                continue;

            var typeArg = attr.ConstructorArguments[0];
            if (typeArg.Kind != TypedConstantKind.Type || typeArg.Value is not INamedTypeSymbol condTypeSymbol)
                continue;

            var condTypeFQN = GetFullyQualifiedName(condTypeSymbol);
            var ctorArgLiterals = ImmutableArray<string>.Empty;

            if (attr.ConstructorArguments.Length > 1)
            {
                var paramsArg = attr.ConstructorArguments[1];
                if (paramsArg.Kind == TypedConstantKind.Array)
                {
                    ctorArgLiterals = paramsArg.Values
                        .Select(SerializeTypedConstant)
                        .Where(s => s is not null)
                        .Select(s => s!)
                        .ToImmutableArray();
                }
            }

            entries.Add(new TerminationConditionEntry(agentTypeName, condTypeFQN, ctorArgLiterals));
        }

        return entries.ToImmutable();
    }

    private static string? SerializeTypedConstant(TypedConstant constant)
    {
        return constant.Kind switch
        {
            TypedConstantKind.Primitive when constant.Value is string s =>
                "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
            TypedConstantKind.Primitive when constant.Value is int i => i.ToString(),
            TypedConstantKind.Primitive when constant.Value is long l => l.ToString() + "L",
            TypedConstantKind.Primitive when constant.Value is bool b => b ? "true" : "false",
            TypedConstantKind.Primitive when constant.Value is null => "null",
            TypedConstantKind.Type when constant.Value is ITypeSymbol ts =>
                $"typeof({GetFullyQualifiedName(ts)})",
            _ => null,
        };
    }

    private static string? GetDescriptionFromAttributes(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attr in attributes)
        {
            if (attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                "global::System.ComponentModel.DescriptionAttribute" &&
                attr.ConstructorArguments.Length == 1 &&
                attr.ConstructorArguments[0].Value is string desc)
                return desc;
        }
        return null;
    }

    private static string GetJsonSchemaType(ITypeSymbol type, out string? itemType)
    {
        itemType = null;
        if (type is INamedTypeSymbol nullable && nullable.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            type = nullable.TypeArguments[0];

        switch (type.SpecialType)
        {
            case SpecialType.System_String: return "string";
            case SpecialType.System_Boolean: return "boolean";
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64: return "integer";
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal: return "number";
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            itemType = GetJsonSchemaType(arrayType.ElementType, out _);
            return "array";
        }

        if (type is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1)
        {
            var baseName = named.ConstructedFrom.ToDisplayString();
            if (baseName == "System.Collections.Generic.IEnumerable<T>" ||
                baseName == "System.Collections.Generic.List<T>" ||
                baseName == "System.Collections.Generic.IReadOnlyList<T>" ||
                baseName == "System.Collections.Generic.ICollection<T>" ||
                baseName == "System.Collections.Generic.IList<T>")
            {
                itemType = GetJsonSchemaType(named.TypeArguments[0], out _);
                return "array";
            }
        }

        return "";
    }

    private static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.DeclaredAccessibility == Accessibility.Private ||
            typeSymbol.DeclaredAccessibility == Accessibility.Protected)
            return false;

        var current = typeSymbol.ContainingType;
        while (current != null)
        {
            if (current.DeclaredAccessibility == Accessibility.Private)
                return false;
            current = current.ContainingType;
        }

        return true;
    }

    private static string GetFullyQualifiedName(ITypeSymbol typeSymbol) =>
        "global::" + typeSymbol.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(
                SymbolDisplayGlobalNamespaceStyle.Omitted));

    private static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Generated";

        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
            else if (c == '.' || c == '-' || c == ' ')
                sb.Append(c == '.' ? '.' : '_');
        }

        var result = sb.ToString();
        var segments = result.Split('.');
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length > 0 && char.IsDigit(segments[i][0]))
                segments[i] = "_" + segments[i];
        }

        return string.Join(".", segments.Where(s => s.Length > 0));
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
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

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
            SourceText.From(GenerateRegistrySource(validFunctionTypes, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentFrameworkFunctionGroups.g.cs",
            SourceText.From(GenerateGroupRegistrySource(groupedByName, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentRegistry.g.cs",
            SourceText.From(GenerateAgentRegistrySource(validAgentTypes, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentHandoffTopologyRegistry.g.cs",
            SourceText.From(GenerateHandoffTopologyRegistrySource(handoffByInitialAgent, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentGroupChatRegistry.g.cs",
            SourceText.From(GenerateGroupChatRegistrySource(groupChatByGroupName, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentSequentialTopologyRegistry.g.cs",
            SourceText.From(GenerateSequentialTopologyRegistrySource(sequenceByPipelineName, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("NeedlrAgentFrameworkBootstrap.g.cs",
            SourceText.From(GenerateBootstrapSource(safeAssemblyName), Encoding.UTF8));

        spc.AddSource("WorkflowFactoryExtensions.g.cs",
            SourceText.From(GenerateWorkflowFactoryExtensionsSource(
                handoffByInitialAgent, groupChatByGroupName, sequenceByPipelineName,
                conditionsByAgentTypeName, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentFactoryExtensions.g.cs",
            SourceText.From(GenerateAgentFactoryExtensionsSource(validAgentTypes, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentTopologyConstants.g.cs",
            SourceText.From(GenerateAgentTopologyConstantsSource(validAgentTypes, allGroupEntries, sequenceByPipelineName, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentFrameworkSyringeExtensions.g.cs",
            SourceText.From(GenerateSyringeExtensionsSource(allGroupEntries, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("GeneratedAIFunctionProvider.g.cs",
            SourceText.From(GenerateAIFunctionProviderSource(validFunctionTypes, safeAssemblyName), Encoding.UTF8));

        configOptions.GlobalOptions.TryGetValue("build_property.NeedlrDiagnostics", out var diagValue);
        if (string.Equals(diagValue, "true", StringComparison.OrdinalIgnoreCase))
        {
            var mermaid = GenerateMermaidDiagram(handoffByInitialAgent, groupChatByGroupName, sequenceByPipelineName);

            spc.AddSource("AgentTopologyGraph.g.cs",
                SourceText.From(GenerateTopologyGraphSource(mermaid, safeAssemblyName), Encoding.UTF8));
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
                SourceText.From(GeneratePartialCompanionSource(agentType, groupedByName), Encoding.UTF8));
        }
    }

    private static string GenerateTopologyGraphSource(
        string diagram,
        string safeAssemblyName)
    {
        var escaped = diagram.Replace("\"", "\"\"");

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {safeAssemblyName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("internal static class AgentTopologyGraphDiagnostics");
        sb.AppendLine("{");
        sb.AppendLine($"    public const string AgentTopologyGraph = @\"{escaped}\";");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateMermaidDiagram(
        Dictionary<(string InitialAgentTypeName, string InitialAgentClassName), List<(string TargetAgentTypeName, string? HandoffReason)>> handoffByInitialAgent,
        Dictionary<string, List<string>> groupChatByGroupName,
        Dictionary<string, List<string>> sequenceByPipelineName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph TD");

        foreach (var kvp in handoffByInitialAgent.OrderBy(k => k.Key.InitialAgentClassName))
        {
            var sourceClass = kvp.Key.InitialAgentClassName;
            foreach (var (targetFqn, reason) in kvp.Value)
            {
                var targetClass = GetShortName(targetFqn);
                if (string.IsNullOrWhiteSpace(reason))
                    sb.AppendLine($"    {sourceClass} --> {targetClass}");
                else
                    sb.AppendLine($"    {sourceClass} -->|\"{EscapeMermaidLabel(reason!)}\"| {targetClass}");
            }
        }

        foreach (var kvp in groupChatByGroupName.OrderBy(k => k.Key))
        {
            sb.AppendLine($"    subgraph GroupChat_{SanitizeMermaidId(kvp.Key)}");
            foreach (var memberFqn in kvp.Value)
                sb.AppendLine($"        {GetShortName(memberFqn)}");
            sb.AppendLine("    end");
        }

        foreach (var kvp in sequenceByPipelineName.OrderBy(k => k.Key))
        {
            sb.AppendLine($"    subgraph Sequential_{SanitizeMermaidId(kvp.Key)}");
            var agents = kvp.Value;
            if (agents.Count == 1)
            {
                sb.AppendLine($"        {GetShortName(agents[0])}");
            }
            else
            {
                for (int i = 0; i < agents.Count - 1; i++)
                {
                    var cur = GetShortName(agents[i]);
                    var next = GetShortName(agents[i + 1]);
                    sb.AppendLine($"        {cur} -->|\"{i + 1}\"| {next}");
                }
            }
            sb.AppendLine("    end");
        }

        return sb.ToString();
    }

    private static string GetShortName(string fqn)
    {
        var stripped = fqn.StartsWith("global::", StringComparison.Ordinal) ? fqn.Substring(8) : fqn;
        var lastDot = stripped.LastIndexOf('.');
        return lastDot >= 0 ? stripped.Substring(lastDot + 1) : stripped;
    }

    private static string SanitizeMermaidId(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    private static string EscapeMermaidLabel(string label) =>
        label.Replace("\"", "&quot;");

    private static string GenerateRegistrySource(
        List<AgentFunctionTypeInfo> types,
        string safeAssemblyName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Needlr AgentFramework Functions");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {safeAssemblyName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated registry for Microsoft Agent Framework function types discovered at compile time.");
        sb.AppendLine("/// Pass <see cref=\"AllFunctionTypes\"/> to");
        sb.AppendLine("/// <c>AgentFrameworkSyringeExtensions.AddAgentFunctionsFromGenerated</c>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.AgentFramework.Generators\", \"1.0.0\")]");
        sb.AppendLine("public static class AgentFrameworkFunctionRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// All types containing methods decorated with [AgentFunction], discovered at compile time.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IReadOnlyList<Type> AllFunctionTypes { get; } = new Type[]");
        sb.AppendLine("    {");

        foreach (var type in types)
        {
            sb.AppendLine($"        typeof({type.TypeName}),");
        }

        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the number of function types discovered at compile time.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static int Count => {types.Count};");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateGroupRegistrySource(
        Dictionary<string, List<string>> groupedByName,
        string safeAssemblyName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Needlr AgentFramework Function Groups");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {safeAssemblyName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated registry for Microsoft Agent Framework function groups discovered at compile time.");
        sb.AppendLine("/// Pass <see cref=\"AllGroups\"/> to");
        sb.AppendLine("/// <c>AgentFrameworkSyringeExtensions.AddAgentFunctionGroupsFromGenerated</c>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.AgentFramework.Generators\", \"1.0.0\")]");
        sb.AppendLine("public static class AgentFrameworkFunctionGroupRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// All function groups, mapping group name to the types in that group, discovered at compile time.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IReadOnlyDictionary<string, IReadOnlyList<Type>> AllGroups { get; } =");
        sb.AppendLine("        new Dictionary<string, IReadOnlyList<Type>>()");
        sb.AppendLine("        {");

        foreach (var kvp in groupedByName.OrderBy(k => k.Key))
        {
            var escapedName = kvp.Key.Replace("\"", "\\\"");
            var typeNames = kvp.Value;
            sb.AppendLine($"            [\"{escapedName}\"] = new Type[]");
            sb.AppendLine("            {");
            foreach (var typeName in typeNames)
            {
                sb.AppendLine($"                typeof({typeName}),");
            }
            sb.AppendLine("            },");
        }

        sb.AppendLine("        };");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateAgentRegistrySource(
        List<NeedlrAiAgentTypeInfo> types,
        string safeAssemblyName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Needlr AgentFramework Agent Registry");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {safeAssemblyName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated registry for agent types declared with [NeedlrAiAgent], discovered at compile time.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.AgentFramework.Generators\", \"1.0.0\")]");
        sb.AppendLine("public static class AgentRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// All types decorated with [NeedlrAiAgent], discovered at compile time.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IReadOnlyList<Type> AllAgentTypes { get; } = new Type[]");
        sb.AppendLine("    {");

        foreach (var type in types)
        {
            sb.AppendLine($"        typeof({type.TypeName}),");
        }

        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the number of agent types discovered at compile time.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static int Count => {types.Count};");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateHandoffTopologyRegistrySource(
        Dictionary<(string InitialAgentTypeName, string InitialAgentClassName), List<(string TargetAgentTypeName, string? HandoffReason)>> handoffByInitialAgent,
        string safeAssemblyName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Needlr AgentFramework Handoff Topology Registry");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {safeAssemblyName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated registry of agent handoff topology declared via [AgentHandoffsTo] attributes.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.AgentFramework.Generators\", \"1.0.0\")]");
        sb.AppendLine("public static class AgentHandoffTopologyRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// All handoff relationships, mapping initial agent type to its declared handoff targets.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IReadOnlyDictionary<Type, IReadOnlyList<(Type TargetType, string? HandoffReason)>> AllHandoffs { get; } =");
        sb.AppendLine("        new Dictionary<Type, IReadOnlyList<(Type, string?)>>()");
        sb.AppendLine("        {");

        foreach (var kvp in handoffByInitialAgent.OrderBy(k => k.Key.InitialAgentClassName))
        {
            sb.AppendLine($"            [typeof({kvp.Key.InitialAgentTypeName})] = new (Type, string?)[]");
            sb.AppendLine("            {");
            foreach (var (targetType, reason) in kvp.Value)
            {
                var reasonLiteral = reason is null ? "null" : $"\"{reason.Replace("\"", "\\\"")}\"";
                sb.AppendLine($"                (typeof({targetType}), {reasonLiteral}),");
            }
            sb.AppendLine("            },");
        }

        sb.AppendLine("        };");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateGroupChatRegistrySource(
        Dictionary<string, List<string>> groupChatByGroupName,
        string safeAssemblyName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Needlr AgentFramework Group Chat Registry");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {safeAssemblyName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated registry of agent group chat memberships declared via [AgentGroupChatMember] attributes.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.AgentFramework.Generators\", \"1.0.0\")]");
        sb.AppendLine("public static class AgentGroupChatRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// All group chat groups, mapping group name to the agent types in that group.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IReadOnlyDictionary<string, IReadOnlyList<Type>> AllGroups { get; } =");
        sb.AppendLine("        new Dictionary<string, IReadOnlyList<Type>>()");
        sb.AppendLine("        {");

        foreach (var kvp in groupChatByGroupName.OrderBy(k => k.Key))
        {
            var escapedName = kvp.Key.Replace("\"", "\\\"");
            sb.AppendLine($"            [\"{escapedName}\"] = new Type[]");
            sb.AppendLine("            {");
            foreach (var typeName in kvp.Value)
                sb.AppendLine($"                typeof({typeName}),");
            sb.AppendLine("            },");
        }

        sb.AppendLine("        };");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateSequentialTopologyRegistrySource(
        Dictionary<string, List<string>> sequenceByPipelineName,
        string safeAssemblyName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Needlr AgentFramework Sequential Pipeline Registry");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {safeAssemblyName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated registry of sequential pipeline memberships declared via [AgentSequenceMember] attributes.");
        sb.AppendLine("/// Agents within each pipeline are stored in ascending order value order.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.AgentFramework.Generators\", \"1.0.0\")]");
        sb.AppendLine("public static class AgentSequentialTopologyRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// All sequential pipelines, mapping pipeline name to the ordered agent types.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IReadOnlyDictionary<string, IReadOnlyList<Type>> AllPipelines { get; } =");
        sb.AppendLine("        new Dictionary<string, IReadOnlyList<Type>>()");
        sb.AppendLine("        {");

        foreach (var kvp in sequenceByPipelineName.OrderBy(k => k.Key))
        {
            var escapedName = kvp.Key.Replace("\"", "\\\"");
            sb.AppendLine($"            [\"{escapedName}\"] = new Type[]");
            sb.AppendLine("            {");
            foreach (var typeName in kvp.Value)
                sb.AppendLine($"                typeof({typeName}),");
            sb.AppendLine("            },");
        }

        sb.AppendLine("        };");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateWorkflowFactoryExtensionsSource(
        Dictionary<(string InitialAgentTypeName, string InitialAgentClassName), List<(string TargetAgentTypeName, string? HandoffReason)>> handoffByInitialAgent,
        Dictionary<string, List<string>> groupChatByGroupName,
        Dictionary<string, List<string>> sequenceByPipelineName,
        Dictionary<string, List<TerminationConditionEntry>> conditionsByAgentTypeName,
        string safeAssemblyName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Needlr AgentFramework IWorkflowFactory Extension Methods");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Agents.AI.Workflows;");
        sb.AppendLine("using NexusLabs.Needlr.AgentFramework;");
        sb.AppendLine();
        sb.AppendLine($"namespace {safeAssemblyName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated strongly-typed extension methods on <see cref=\"IWorkflowFactory\"/>.");
        sb.AppendLine("/// Each method encapsulates an agent type or group name so the composition root");
        sb.AppendLine("/// requires no direct agent type references or magic strings.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.AgentFramework.Generators\", \"1.0.0\")]");
        sb.AppendLine("public static partial class WorkflowFactoryExtensions");
        sb.AppendLine("{");

        foreach (var kvp in handoffByInitialAgent.OrderBy(k => k.Key.InitialAgentClassName))
        {
            var className = kvp.Key.InitialAgentClassName;
            var typeName = kvp.Key.InitialAgentTypeName;
            var methodName = $"Create{StripAgentSuffix(className)}HandoffWorkflow";

            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Creates a handoff workflow starting from <see cref=\"{typeName.Replace("global::", "")}\"/>.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <remarks>");
            sb.AppendLine($"    /// Handoff targets declared via <see cref=\"global::NexusLabs.Needlr.AgentFramework.AgentHandoffsToAttribute\"/>:");
            sb.AppendLine($"    /// <list type=\"bullet\">");
            foreach (var (targetTypeName, reason) in kvp.Value)
            {
                var cref = targetTypeName.Replace("global::", "");
                if (string.IsNullOrEmpty(reason))
                    sb.AppendLine($"    /// <item><description><see cref=\"{cref}\"/></description></item>");
                else
                    sb.AppendLine($"    /// <item><description><see cref=\"{cref}\"/> — {reason}</description></item>");
            }
            sb.AppendLine($"    /// </list>");
            sb.AppendLine($"    /// </remarks>");
            sb.AppendLine($"    public static Workflow {methodName}(this IWorkflowFactory workflowFactory)");
            sb.AppendLine($"        => workflowFactory.CreateHandoffWorkflow<{typeName}>();");
            sb.AppendLine();

            var allHandoffAgents = new[] { typeName }
                .Concat(kvp.Value.Select(v => v.TargetAgentTypeName))
                .ToList();
            var handoffConditions = allHandoffAgents
                .SelectMany(t => conditionsByAgentTypeName.TryGetValue(t, out var c) ? c : Enumerable.Empty<TerminationConditionEntry>())
                .ToList();

            if (handoffConditions.Count > 0)
            {
                var runMethodName = $"Run{StripAgentSuffix(className)}HandoffWorkflowAsync";
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Creates and runs the handoff workflow starting from <see cref=\"{typeName.Replace("global::", "")}\"/>, applying declared termination conditions.");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <remarks>");
                sb.AppendLine($"    /// Termination conditions are evaluated after each completed agent turn (Layer 2).");
                sb.AppendLine($"    /// The workflow stops early when any condition is satisfied.");
                sb.AppendLine($"    /// </remarks>");
                sb.AppendLine($"    /// <param name=\"workflowFactory\">The workflow factory.</param>");
                sb.AppendLine($"    /// <param name=\"message\">The input message to start the workflow.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Optional cancellation token.</param>");
                sb.AppendLine($"    /// <returns>A dictionary mapping executor IDs to their response text.</returns>");
                sb.AppendLine($"    public static async global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyDictionary<string, string>> {runMethodName}(");
                sb.AppendLine($"        this IWorkflowFactory workflowFactory,");
                sb.AppendLine($"        string message,");
                sb.AppendLine($"        global::System.Threading.CancellationToken cancellationToken = default)");
                sb.AppendLine($"    {{");
                sb.AppendLine($"        var workflow = workflowFactory.{methodName}();");
                sb.AppendLine($"        global::System.Collections.Generic.IReadOnlyList<global::NexusLabs.Needlr.AgentFramework.IWorkflowTerminationCondition> conditions =");
                sb.AppendLine($"            new global::System.Collections.Generic.List<global::NexusLabs.Needlr.AgentFramework.IWorkflowTerminationCondition>");
                sb.AppendLine($"            {{");
                foreach (var cond in handoffConditions)
                {
                    var args = cond.CtorArgLiterals.IsEmpty ? "" : string.Join(", ", cond.CtorArgLiterals);
                    sb.AppendLine($"                new {cond.ConditionTypeFQN}({args}),");
                }
                sb.AppendLine($"            }};");
                sb.AppendLine($"        return await global::NexusLabs.Needlr.AgentFramework.Workflows.StreamingRunWorkflowExtensions.RunAsync(workflow, message, conditions, cancellationToken).ConfigureAwait(false);");
                sb.AppendLine($"    }}");
                sb.AppendLine();
            }
        }

        foreach (var kvp in groupChatByGroupName.OrderBy(k => k.Key))
        {
            var groupName = kvp.Key;
            var methodSuffix = GroupNameToPascalCase(groupName);
            var methodName = $"Create{methodSuffix}GroupChatWorkflow";
            var escapedGroupName = groupName.Replace("\"", "\\\"");

            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Creates a round-robin group chat workflow for the \"{escapedGroupName}\" group.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <remarks>");
            sb.AppendLine($"    /// Participants declared via <see cref=\"global::NexusLabs.Needlr.AgentFramework.AgentGroupChatMemberAttribute\"/>:");
            sb.AppendLine($"    /// <list type=\"bullet\">");
            foreach (var participantTypeName in kvp.Value)
            {
                var cref = participantTypeName.Replace("global::", "");
                sb.AppendLine($"    /// <item><description><see cref=\"{cref}\"/></description></item>");
            }
            sb.AppendLine($"    /// </list>");
            sb.AppendLine($"    /// </remarks>");
            sb.AppendLine($"    public static Workflow {methodName}(this IWorkflowFactory workflowFactory, int maxIterations = 10)");
            sb.AppendLine($"        => workflowFactory.CreateGroupChatWorkflow(\"{escapedGroupName}\", maxIterations);");
            sb.AppendLine();

            var gcConditions = kvp.Value
                .SelectMany(t => conditionsByAgentTypeName.TryGetValue(t, out var c) ? c : Enumerable.Empty<TerminationConditionEntry>())
                .ToList();

            if (gcConditions.Count > 0)
            {
                var runMethodName = $"Run{methodSuffix}GroupChatWorkflowAsync";
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Creates and runs the \"{escapedGroupName}\" group chat workflow, applying declared termination conditions.");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <remarks>");
                sb.AppendLine($"    /// Termination conditions are evaluated after each completed agent turn (Layer 2).");
                sb.AppendLine($"    /// The workflow stops early when any condition is satisfied.");
                sb.AppendLine($"    /// </remarks>");
                sb.AppendLine($"    /// <param name=\"workflowFactory\">The workflow factory.</param>");
                sb.AppendLine($"    /// <param name=\"message\">The input message to start the workflow.</param>");
                sb.AppendLine($"    /// <param name=\"maxIterations\">Maximum number of group chat iterations.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Optional cancellation token.</param>");
                sb.AppendLine($"    /// <returns>A dictionary mapping executor IDs to their response text.</returns>");
                sb.AppendLine($"    public static async global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyDictionary<string, string>> {runMethodName}(");
                sb.AppendLine($"        this IWorkflowFactory workflowFactory,");
                sb.AppendLine($"        string message,");
                sb.AppendLine($"        int maxIterations = 10,");
                sb.AppendLine($"        global::System.Threading.CancellationToken cancellationToken = default)");
                sb.AppendLine($"    {{");
                sb.AppendLine($"        var workflow = workflowFactory.{methodName}(maxIterations);");
                sb.AppendLine($"        global::System.Collections.Generic.IReadOnlyList<global::NexusLabs.Needlr.AgentFramework.IWorkflowTerminationCondition> conditions =");
                sb.AppendLine($"            new global::System.Collections.Generic.List<global::NexusLabs.Needlr.AgentFramework.IWorkflowTerminationCondition>");
                sb.AppendLine($"            {{");
                foreach (var cond in gcConditions)
                {
                    var args = cond.CtorArgLiterals.IsEmpty ? "" : string.Join(", ", cond.CtorArgLiterals);
                    sb.AppendLine($"                new {cond.ConditionTypeFQN}({args}),");
                }
                sb.AppendLine($"            }};");
                sb.AppendLine($"        return await global::NexusLabs.Needlr.AgentFramework.Workflows.StreamingRunWorkflowExtensions.RunAsync(workflow, message, conditions, cancellationToken).ConfigureAwait(false);");
                sb.AppendLine($"    }}");
                sb.AppendLine();
            }
        }

        foreach (var kvp in sequenceByPipelineName.OrderBy(k => k.Key))
        {
            var pipelineName = kvp.Key;
            var methodSuffix = GroupNameToPascalCase(pipelineName);
            var methodName = $"Create{methodSuffix}SequentialWorkflow";
            var escapedPipelineName = pipelineName.Replace("\"", "\\\"");

            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Creates a sequential pipeline workflow for the \"{escapedPipelineName}\" pipeline.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    /// <remarks>");
            sb.AppendLine($"    /// Agents declared via <see cref=\"global::NexusLabs.Needlr.AgentFramework.AgentSequenceMemberAttribute\"/> (in order):");
            sb.AppendLine($"    /// <list type=\"number\">");
            foreach (var memberTypeName in kvp.Value)
            {
                var cref = memberTypeName.Replace("global::", "");
                sb.AppendLine($"    /// <item><description><see cref=\"{cref}\"/></description></item>");
            }
            sb.AppendLine($"    /// </list>");
            sb.AppendLine($"    /// </remarks>");
            sb.AppendLine($"    public static Workflow {methodName}(this IWorkflowFactory workflowFactory)");
            sb.AppendLine($"        => workflowFactory.CreateSequentialWorkflow(\"{escapedPipelineName}\");");
            sb.AppendLine();

            var seqConditions = kvp.Value
                .SelectMany(t => conditionsByAgentTypeName.TryGetValue(t, out var c) ? c : Enumerable.Empty<TerminationConditionEntry>())
                .ToList();

            if (seqConditions.Count > 0)
            {
                var runMethodName = $"Run{methodSuffix}SequentialWorkflowAsync";
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Creates and runs the \"{escapedPipelineName}\" sequential workflow, applying declared termination conditions.");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    /// <remarks>");
                sb.AppendLine($"    /// Termination conditions are evaluated after each completed agent turn (Layer 2).");
                sb.AppendLine($"    /// The workflow stops early when any condition is satisfied.");
                sb.AppendLine($"    /// </remarks>");
                sb.AppendLine($"    /// <param name=\"workflowFactory\">The workflow factory.</param>");
                sb.AppendLine($"    /// <param name=\"message\">The input message to start the workflow.</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Optional cancellation token.</param>");
                sb.AppendLine($"    /// <returns>A dictionary mapping executor IDs to their response text.</returns>");
                sb.AppendLine($"    public static async global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyDictionary<string, string>> {runMethodName}(");
                sb.AppendLine($"        this IWorkflowFactory workflowFactory,");
                sb.AppendLine($"        string message,");
                sb.AppendLine($"        global::System.Threading.CancellationToken cancellationToken = default)");
                sb.AppendLine($"    {{");
                sb.AppendLine($"        var workflow = workflowFactory.{methodName}();");
                sb.AppendLine($"        global::System.Collections.Generic.IReadOnlyList<global::NexusLabs.Needlr.AgentFramework.IWorkflowTerminationCondition> conditions =");
                sb.AppendLine($"            new global::System.Collections.Generic.List<global::NexusLabs.Needlr.AgentFramework.IWorkflowTerminationCondition>");
                sb.AppendLine($"            {{");
                foreach (var cond in seqConditions)
                {
                    var args = cond.CtorArgLiterals.IsEmpty ? "" : string.Join(", ", cond.CtorArgLiterals);
                    sb.AppendLine($"                new {cond.ConditionTypeFQN}({args}),");
                }
                sb.AppendLine($"            }};");
                sb.AppendLine($"        return await global::NexusLabs.Needlr.AgentFramework.Workflows.StreamingRunWorkflowExtensions.RunAsync(workflow, message, conditions, cancellationToken).ConfigureAwait(false);");
                sb.AppendLine($"    }}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateAgentFactoryExtensionsSource(
        List<NeedlrAiAgentTypeInfo> agents,
        string safeAssemblyName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Needlr AgentFramework IAgentFactory Extension Methods");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Agents.AI;");
        sb.AppendLine("using NexusLabs.Needlr.AgentFramework;");
        sb.AppendLine();
        sb.AppendLine($"namespace {safeAssemblyName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated strongly-typed extension methods on <see cref=\"IAgentFactory\"/>.");
        sb.AppendLine("/// Each method creates an agent from its <c>[NeedlrAiAgent]</c> declaration,");
        sb.AppendLine("/// eliminating magic strings and direct type references at the composition root.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.AgentFramework.Generators\", \"1.0.0\")]");
        sb.AppendLine("public static class GeneratedAgentFactoryExtensions");
        sb.AppendLine("{");

        foreach (var agent in agents.OrderBy(a => a.ClassName))
        {
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Creates an <see cref=\"AIAgent\"/> configured for <see cref=\"{agent.TypeName.Replace("global::", "")}\"/>.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static AIAgent Create{agent.ClassName}(this IAgentFactory factory)");
            sb.AppendLine($"        => factory.CreateAgent<{agent.TypeName}>();");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateAgentTopologyConstantsSource(
        List<NeedlrAiAgentTypeInfo> agents,
        List<AgentFunctionGroupEntry> groupEntries,
        Dictionary<string, List<string>> sequenceByPipelineName,
        string safeAssemblyName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Needlr AgentFramework Topology Constants");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {safeAssemblyName}.Generated;");
        sb.AppendLine();

        sb.AppendLine("/// <summary>String constants for agent type names discovered at compile time.</summary>");
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.AgentFramework.Generators\", \"1.0.0\")]");
        sb.AppendLine("public static class AgentNames");
        sb.AppendLine("{");
        foreach (var agent in agents.OrderBy(a => a.ClassName))
        {
            sb.AppendLine($"    /// <summary>The name of <see cref=\"{agent.TypeName.Replace("global::", "")}\"/>.</summary>");
            sb.AppendLine($"    public const string {agent.ClassName} = \"{agent.ClassName}\";");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        var groupNames = groupEntries.Select(e => e.GroupName).Distinct().OrderBy(g => g).ToList();
        sb.AppendLine("/// <summary>String constants for function group names discovered at compile time.</summary>");
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.AgentFramework.Generators\", \"1.0.0\")]");
        sb.AppendLine("public static class GroupNames");
        sb.AppendLine("{");
        foreach (var gn in groupNames)
        {
            var propName = GroupNameToPascalCase(gn);
            var escaped = gn.Replace("\"", "\\\"");
            sb.AppendLine($"    /// <summary>The group name <c>\"{escaped}\"</c>.</summary>");
            sb.AppendLine($"    public const string {propName} = \"{escaped}\";");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("/// <summary>String constants for sequential pipeline names discovered at compile time.</summary>");
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.AgentFramework.Generators\", \"1.0.0\")]");
        sb.AppendLine("public static class PipelineNames");
        sb.AppendLine("{");
        foreach (var pn in sequenceByPipelineName.Keys.OrderBy(k => k))
        {
            var propName = GroupNameToPascalCase(pn);
            var escaped = pn.Replace("\"", "\\\"");
            sb.AppendLine($"    /// <summary>The pipeline name <c>\"{escaped}\"</c>.</summary>");
            sb.AppendLine($"    public const string {propName} = \"{escaped}\";");
        }
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateSyringeExtensionsSource(
        List<AgentFunctionGroupEntry> groupEntries,
        string safeAssemblyName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Needlr AgentFramework AgentFrameworkSyringe Extension Methods");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
        sb.AppendLine("using NexusLabs.Needlr.AgentFramework;");
        sb.AppendLine();
        sb.AppendLine($"namespace {safeAssemblyName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Generated strongly-typed extension methods on <see cref=\"AgentFrameworkSyringe\"/>.");
        sb.AppendLine("/// Each method registers a named function group without requiring direct type references.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.AgentFramework.Generators\", \"1.0.0\")]");
        sb.AppendLine("public static class GeneratedAgentFrameworkSyringeExtensions");
        sb.AppendLine("{");

        var byGroupName = groupEntries
            .GroupBy(e => e.GroupName)
            .OrderBy(g => g.Key);

        foreach (var grp in byGroupName)
        {
            var methodName = $"With{GroupNameToPascalCase(grp.Key)}";
            var escapedGroupName = grp.Key.Replace("\"", "\\\"");
            var types = grp.Select(e => e.TypeName).Distinct().ToList();

            sb.AppendLine($"    /// <summary>Registers the '{escapedGroupName}' function group with the syringe.</summary>");
            sb.AppendLine($"    [RequiresUnreferencedCode(\"AgentFramework function setup uses reflection to discover [AgentFunction] methods.\")]");
            sb.AppendLine($"    [RequiresDynamicCode(\"AgentFramework function setup uses reflection APIs that require dynamic code generation.\")]");
            sb.AppendLine($"    public static AgentFrameworkSyringe {methodName}(this AgentFrameworkSyringe syringe)");
            sb.AppendLine($"        => syringe.AddAgentFunctions(new global::System.Type[] {{ {string.Join(", ", types.Select(t => $"typeof({t})"))} }});");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateAIFunctionProviderSource(
        List<AgentFunctionTypeInfo> types,
        string safeAssemblyName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using global::Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine($"namespace {safeAssemblyName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.AgentFramework.Generators\", \"1.0.0\")]");
        sb.AppendLine("internal sealed class GeneratedAIFunctionProvider : global::NexusLabs.Needlr.AgentFramework.IAIFunctionProvider");
        sb.AppendLine("{");
        sb.AppendLine("    public bool TryGetFunctions(");
        sb.AppendLine("        global::System.Type functionType,");
        sb.AppendLine("        global::System.IServiceProvider serviceProvider,");
        sb.AppendLine("        [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)]");
        sb.AppendLine("        out global::System.Collections.Generic.IReadOnlyList<global::Microsoft.Extensions.AI.AIFunction>? functions)");
        sb.AppendLine("    {");

        if (types.Count == 0)
        {
            sb.AppendLine("        functions = null;");
            sb.AppendLine("        return false;");
        }
        else
        {
            var first = true;
            foreach (var type in types)
            {
                var keyword = first ? "if" : "else if";
                first = false;
                sb.AppendLine($"        {keyword} (functionType == typeof({type.TypeName}))");
                sb.AppendLine("        {");
                if (!type.IsStatic)
                    sb.AppendLine($"            var typed = serviceProvider.GetRequiredService<{type.TypeName}>();");
                sb.AppendLine("            functions = new global::System.Collections.Generic.List<global::Microsoft.Extensions.AI.AIFunction>");
                sb.AppendLine("            {");
                foreach (var m in type.Methods)
                {
                    var nestedName = $"{GetShortName(type.TypeName)}_{m.MethodName}";
                    if (type.IsStatic)
                        sb.AppendLine($"                new {nestedName}(),");
                    else
                        sb.AppendLine($"                new {nestedName}(typed),");
                }
                sb.AppendLine("            }.AsReadOnly();");
                sb.AppendLine("            return true;");
                sb.AppendLine("        }");
            }
            sb.AppendLine("        functions = null;");
            sb.AppendLine("        return false;");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        foreach (var type in types)
        {
            var shortTypeName = GetShortName(type.TypeName);
            foreach (var m in type.Methods)
            {
                var nestedName = $"{shortTypeName}_{m.MethodName}";
                AppendAIFunctionNestedClass(sb, type, m, nestedName);
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendAIFunctionNestedClass(
        StringBuilder sb,
        AgentFunctionTypeInfo type,
        AgentFunctionMethodInfo method,
        string nestedClassName)
    {
        sb.AppendLine($"    private sealed class {nestedClassName} : global::Microsoft.Extensions.AI.AIFunction");
        sb.AppendLine("    {");

        if (!type.IsStatic)
            sb.AppendLine($"        private readonly {type.TypeName} _instance;");

        var schemaJson = BuildJsonSchema(method.Parameters);
        sb.AppendLine("        private static readonly global::System.Text.Json.JsonElement _schema =");
        sb.AppendLine($"            global::System.Text.Json.JsonDocument.Parse(\"\"\"{schemaJson}\"\"\").RootElement.Clone();");
        sb.AppendLine();

        if (!type.IsStatic)
            sb.AppendLine($"        public {nestedClassName}({type.TypeName} instance) {{ _instance = instance; }}");
        else
            sb.AppendLine($"        public {nestedClassName}() {{ }}");

        sb.AppendLine();

        var escapedName = method.MethodName.Replace("\"", "\\\"");
        var escapedDesc = method.Description.Replace("\\", "\\\\").Replace("\"", "\\\"");
        sb.AppendLine($"        public override string Name => \"{escapedName}\";");
        sb.AppendLine($"        public override string Description => \"{escapedDesc}\";");
        sb.AppendLine("        public override global::System.Text.Json.JsonElement JsonSchema => _schema;");
        sb.AppendLine();

        if (method.IsAsync)
            sb.AppendLine("        protected override async global::System.Threading.Tasks.ValueTask<object?> InvokeCoreAsync(");
        else
            sb.AppendLine("        protected override global::System.Threading.Tasks.ValueTask<object?> InvokeCoreAsync(");

        sb.AppendLine("            global::Microsoft.Extensions.AI.AIFunctionArguments arguments,");
        sb.AppendLine("            global::System.Threading.CancellationToken ct)");
        sb.AppendLine("        {");

        foreach (var param in method.Parameters)
        {
            if (param.IsCancellationToken)
                continue;
            AppendParameterExtraction(sb, param);
        }

        var paramList = string.Join(", ", method.Parameters.Select(p => p.IsCancellationToken ? "ct" : p.Name));
        var instanceExpr = type.IsStatic ? type.TypeName : "_instance";

        if (method.IsAsync)
        {
            if (method.IsVoidLike)
            {
                sb.AppendLine($"            await {instanceExpr}.{method.MethodName}({paramList}).ConfigureAwait(false);");
                sb.AppendLine("            return null;");
            }
            else
            {
                sb.AppendLine($"            var result = await {instanceExpr}.{method.MethodName}({paramList}).ConfigureAwait(false);");
                sb.AppendLine("            return result;");
            }
        }
        else
        {
            if (method.IsVoidLike)
            {
                sb.AppendLine($"            {instanceExpr}.{method.MethodName}({paramList});");
                sb.AppendLine("            return global::System.Threading.Tasks.ValueTask.FromResult<object?>(null);");
            }
            else
            {
                sb.AppendLine($"            var result = {instanceExpr}.{method.MethodName}({paramList});");
                sb.AppendLine("            return global::System.Threading.Tasks.ValueTask.FromResult<object?>(result);");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static string BuildJsonSchema(ImmutableArray<AgentFunctionParameterInfo> parameters)
    {
        var nonCtParams = parameters.Where(p => !p.IsCancellationToken).ToList();
        if (nonCtParams.Count == 0)
            return "{\"type\":\"object\",\"properties\":{}}";

        var props = new StringBuilder();
        var required = new List<string>();

        props.Append("{");
        var firstProp = true;
        foreach (var param in nonCtParams)
        {
            if (!firstProp) props.Append(",");
            firstProp = false;
            var escapedParamName = param.Name.Replace("\"", "\\\"");
            props.Append($"\"{escapedParamName}\":");
            props.Append(BuildJsonSchemaTypeEntry(param));
            if (param.IsRequired)
                required.Add(param.Name);
        }
        props.Append("}");

        var sb = new StringBuilder();
        sb.Append("{\"type\":\"object\",\"properties\":");
        sb.Append(props);
        if (required.Count > 0)
        {
            sb.Append(",\"required\":[");
            sb.Append(string.Join(",", required.Select(r => "\"" + r.Replace("\"", "\\\"") + "\"")));
            sb.Append("]");
        }
        sb.Append("}");
        return sb.ToString();
    }

    private static string BuildJsonSchemaTypeEntry(AgentFunctionParameterInfo param)
    {
        if (string.IsNullOrEmpty(param.JsonSchemaType))
            return "{}";

        if (param.JsonSchemaType == "array")
        {
            var desc = string.IsNullOrEmpty(param.Description)
                ? ""
                : $",\"description\":\"{param.Description!.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
            if (!string.IsNullOrEmpty(param.ItemJsonSchemaType))
                return $"{{\"type\":\"array\",\"items\":{{\"type\":\"{param.ItemJsonSchemaType}\"}}{desc}}}";
            return $"{{\"type\":\"array\"{desc}}}";
        }

        if (!string.IsNullOrEmpty(param.Description))
        {
            var escapedDesc = param.Description!.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"{{\"type\":\"{param.JsonSchemaType}\",\"description\":\"{escapedDesc}\"}}";
        }

        return $"{{\"type\":\"{param.JsonSchemaType}\"}}";
    }

    private static void AppendParameterExtraction(StringBuilder sb, AgentFunctionParameterInfo param)
    {
        var rawVar = $"_raw_{param.Name}";
        var jVar = $"_j_{param.Name}";
        sb.AppendLine($"            arguments.TryGetValue(\"{param.Name}\", out var {rawVar});");

        switch (param.JsonSchemaType)
        {
            case "string":
                sb.AppendLine($"            var {param.Name} = {rawVar} is global::System.Text.Json.JsonElement {jVar} ? {jVar}.GetString() ?? \"\" : {rawVar}?.ToString() ?? \"\";");
                break;
            case "boolean":
            {
                var bVar = $"_b_{param.Name}";
                sb.AppendLine($"            var {param.Name} = {rawVar} is global::System.Text.Json.JsonElement {jVar} ? {jVar}.GetBoolean() : {rawVar} is bool {bVar} ? {bVar} : global::System.Convert.ToBoolean({rawVar});");
                break;
            }
            case "integer":
                AppendIntegerExtraction(sb, param, rawVar, jVar);
                break;
            case "number":
                AppendNumberExtraction(sb, param, rawVar, jVar);
                break;
            default:
            {
                var cVar = $"_c_{param.Name}";
                var nullSuppress = param.IsNullable ? "" : "!";
                sb.AppendLine($"            var {param.Name} = {rawVar} is {param.TypeFullName} {cVar} ? {cVar} : default({param.TypeFullName}){nullSuppress};");
                break;
            }
        }
    }

    private static void AppendIntegerExtraction(StringBuilder sb, AgentFunctionParameterInfo param, string rawVar, string jVar)
    {
        var typeFqn = param.TypeFullName;
        string getMethod, castType, convertMethod;
        var iVar = $"_i_{param.Name}";

        if (typeFqn.Contains("System.Int64"))
        { getMethod = "GetInt64()"; castType = "long"; convertMethod = "ToInt64"; }
        else if (typeFqn.Contains("System.Int16"))
        { getMethod = "GetInt16()"; castType = "short"; convertMethod = "ToInt16"; }
        else if (typeFqn.Contains("System.SByte"))
        { getMethod = "GetSByte()"; castType = "sbyte"; convertMethod = "ToSByte"; }
        else if (typeFqn.Contains("System.Byte"))
        { getMethod = "GetByte()"; castType = "byte"; convertMethod = "ToByte"; }
        else if (typeFqn.Contains("System.UInt64"))
        { getMethod = "GetUInt64()"; castType = "ulong"; convertMethod = "ToUInt64"; }
        else if (typeFqn.Contains("System.UInt32"))
        { getMethod = "GetUInt32()"; castType = "uint"; convertMethod = "ToUInt32"; }
        else if (typeFqn.Contains("System.UInt16"))
        { getMethod = "GetUInt16()"; castType = "ushort"; convertMethod = "ToUInt16"; }
        else
        { getMethod = "GetInt32()"; castType = "int"; convertMethod = "ToInt32"; }

        sb.AppendLine($"            var {param.Name} = {rawVar} is global::System.Text.Json.JsonElement {jVar} ? {jVar}.{getMethod} : {rawVar} is {castType} {iVar} ? {iVar} : global::System.Convert.{convertMethod}({rawVar});");
    }

    private static void AppendNumberExtraction(StringBuilder sb, AgentFunctionParameterInfo param, string rawVar, string jVar)
    {
        var typeFqn = param.TypeFullName;
        string getMethod, castType, convertMethod;
        var nVar = $"_n_{param.Name}";

        if (typeFqn.Contains("System.Single"))
        { getMethod = "GetSingle()"; castType = "float"; convertMethod = "ToSingle"; }
        else if (typeFqn.Contains("System.Decimal"))
        { getMethod = "GetDecimal()"; castType = "decimal"; convertMethod = "ToDecimal"; }
        else
        { getMethod = "GetDouble()"; castType = "double"; convertMethod = "ToDouble"; }

        sb.AppendLine($"            var {param.Name} = {rawVar} is global::System.Text.Json.JsonElement {jVar} ? {jVar}.{getMethod} : {rawVar} is {castType} {nVar} ? {nVar} : global::System.Convert.{convertMethod}({rawVar});");
    }

    private static string StripAgentSuffix(string className)
    {
        const string suffix = "Agent";
        return className.EndsWith(suffix, StringComparison.Ordinal) && className.Length > suffix.Length
            ? className.Substring(0, className.Length - suffix.Length)
            : className;
    }

    private static string GroupNameToPascalCase(string groupName)
    {
        var sb = new StringBuilder();
        var capitalizeNext = true;
        foreach (var c in groupName)
        {
            if (c == '-' || c == '_' || c == ' ')
            {
                capitalizeNext = true;
            }
            else if (char.IsLetterOrDigit(c))
            {
                sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }
        }
        return sb.ToString();
    }

    private static string GenerateBootstrapSource(string safeAssemblyName)
    {
        return $@"// <auto-generated/>
// Needlr AgentFramework Module Initializer
#nullable enable

using System.Runtime.CompilerServices;

namespace {safeAssemblyName}.Generated;

/// <summary>
/// Auto-registers source-generated Agent Framework types with the Needlr bootstrap.
/// Fires automatically when the assembly loads — no explicit <c>Add*FromGenerated()</c> calls needed.
/// </summary>
[global::System.CodeDom.Compiler.GeneratedCodeAttribute(""NexusLabs.Needlr.AgentFramework.Generators"", ""1.0.0"")]
internal static class NeedlrAgentFrameworkModuleInitializer
{{
    [ModuleInitializer]
    internal static void Initialize()
    {{
        global::NexusLabs.Needlr.AgentFramework.AgentFrameworkGeneratedBootstrap.Register(
            () => AgentFrameworkFunctionRegistry.AllFunctionTypes,
            () => AgentFrameworkFunctionGroupRegistry.AllGroups,
            () => AgentRegistry.AllAgentTypes,
            () => AgentHandoffTopologyRegistry.AllHandoffs,
            () => AgentGroupChatRegistry.AllGroups,
            () => AgentSequentialTopologyRegistry.AllPipelines);
        global::NexusLabs.Needlr.AgentFramework.AgentFrameworkGeneratedBootstrap.RegisterAIFunctionProvider(
            new global::{safeAssemblyName}.Generated.GeneratedAIFunctionProvider());
    }}
}}
";
    }

    private static string GeneratePartialCompanionSource(
        NeedlrAiAgentTypeInfo agentType,
        Dictionary<string, List<string>> groupedByName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (agentType.NamespaceName is not null)
        {
            sb.AppendLine($"namespace {agentType.NamespaceName};");
            sb.AppendLine();
        }

        sb.AppendLine($"partial class {agentType.ClassName}");
        sb.AppendLine("{");

        sb.AppendLine("    /// <summary>The declared name of this agent, equal to the class name.</summary>");

        var toolsDocLines = BuildToolsDocComment(agentType, groupedByName);
        foreach (var line in toolsDocLines)
            sb.AppendLine(line);

        sb.AppendLine($"    public static string AgentName => nameof({agentType.ClassName});");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static List<string> BuildToolsDocComment(
        NeedlrAiAgentTypeInfo agentType,
        Dictionary<string, List<string>> groupedByName)
    {
        static string ShortName(string fqn)
        {
            var clean = fqn.StartsWith("global::") ? fqn.Substring(8) : fqn;
            var dot = clean.LastIndexOf('.');
            return dot >= 0 ? clean.Substring(dot + 1) : clean;
        }

        var lines = new List<string>();

        // FunctionTypes = new Type[0] — explicitly no tools
        if (agentType.HasExplicitFunctionTypes && agentType.ExplicitFunctionTypeFQNs.IsEmpty
            && agentType.FunctionGroupNames.IsEmpty)
        {
            lines.Add("    /// <remarks>This agent has no tools assigned (declared with an empty <c>FunctionTypes</c>).</remarks>");
            return lines;
        }

        // Neither set — uses all registered function types
        if (!agentType.HasExplicitFunctionTypes && agentType.FunctionGroupNames.IsEmpty)
        {
            lines.Add("    /// <remarks>This agent uses all registered function types.</remarks>");
            return lines;
        }

        // Build the resolved list of function types
        var entries = new List<(string displayName, string? groupSource)>();

        foreach (var group in agentType.FunctionGroupNames)
        {
            if (groupedByName.TryGetValue(group, out var types))
            {
                foreach (var typeFqn in types)
                    entries.Add((ShortName(typeFqn), group));
            }
            else
            {
                entries.Add(($"(unresolved group \"{group}\")", group));
            }
        }

        foreach (var typeFqn in agentType.ExplicitFunctionTypeFQNs)
        {
            var shortName = ShortName(typeFqn);
            if (!entries.Any(e => e.displayName == shortName))
                entries.Add((shortName, null));
        }

        lines.Add("    /// <remarks>");
        lines.Add("    /// <para>Agent tools:</para>");
        lines.Add("    /// <list type=\"bullet\">");
        foreach (var (displayName, groupSource) in entries)
        {
            var source = groupSource is not null ? $" (group <c>\"{groupSource}\"</c>)" : " (explicit type)";
            lines.Add($"    /// <item><term><see cref=\"{displayName}\"/>{source}</term></item>");
        }
        lines.Add("    /// </list>");
        lines.Add("    /// </remarks>");

        return lines;
    }

    private readonly struct AgentFunctionTypeInfo
    {
        public AgentFunctionTypeInfo(string typeName, string assemblyName, bool isStatic, ImmutableArray<AgentFunctionMethodInfo> methods)
        {
            TypeName = typeName; AssemblyName = assemblyName; IsStatic = isStatic; Methods = methods;
        }

        public string TypeName { get; }
        public string AssemblyName { get; }
        public bool IsStatic { get; }
        public ImmutableArray<AgentFunctionMethodInfo> Methods { get; }
    }

    private readonly struct AgentFunctionGroupEntry
    {
        public AgentFunctionGroupEntry(string typeName, string groupName)
        {
            TypeName = typeName;
            GroupName = groupName;
        }

        public string TypeName { get; }
        public string GroupName { get; }
    }

    private readonly struct NeedlrAiAgentTypeInfo
    {
        public NeedlrAiAgentTypeInfo(
            string typeName,
            string className,
            string? namespaceName,
            bool isPartial,
            ImmutableArray<string> functionGroupNames,
            ImmutableArray<string> explicitFunctionTypeFQNs,
            bool hasExplicitFunctionTypes)
        {
            TypeName = typeName;
            ClassName = className;
            NamespaceName = namespaceName;
            IsPartial = isPartial;
            FunctionGroupNames = functionGroupNames;
            ExplicitFunctionTypeFQNs = explicitFunctionTypeFQNs;
            HasExplicitFunctionTypes = hasExplicitFunctionTypes;
        }

        public string TypeName { get; }
        public string ClassName { get; }
        public string? NamespaceName { get; }
        public bool IsPartial { get; }
        public ImmutableArray<string> FunctionGroupNames { get; }
        public ImmutableArray<string> ExplicitFunctionTypeFQNs { get; }
        public bool HasExplicitFunctionTypes { get; }
    }

    private readonly struct HandoffEntry
    {
        public HandoffEntry(string initialAgentTypeName, string initialAgentClassName, string targetAgentTypeName, string? handoffReason)
        {
            InitialAgentTypeName = initialAgentTypeName;
            InitialAgentClassName = initialAgentClassName;
            TargetAgentTypeName = targetAgentTypeName;
            HandoffReason = handoffReason;
        }

        public string InitialAgentTypeName { get; }
        public string InitialAgentClassName { get; }
        public string TargetAgentTypeName { get; }
        public string? HandoffReason { get; }
    }

    private readonly struct GroupChatEntry
    {
        public GroupChatEntry(string agentTypeName, string groupName)
        {
            AgentTypeName = agentTypeName;
            GroupName = groupName;
        }

        public string AgentTypeName { get; }
        public string GroupName { get; }
    }

    private readonly struct SequenceEntry
    {
        public SequenceEntry(string agentTypeName, string pipelineName, int order)
        {
            AgentTypeName = agentTypeName;
            PipelineName = pipelineName;
            Order = order;
        }

        public string AgentTypeName { get; }
        public string PipelineName { get; }
        public int Order { get; }
    }

    private readonly struct TerminationConditionEntry
    {
        public TerminationConditionEntry(string agentTypeName, string conditionTypeFQN, ImmutableArray<string> ctorArgLiterals)
        {
            AgentTypeName = agentTypeName;
            ConditionTypeFQN = conditionTypeFQN;
            CtorArgLiterals = ctorArgLiterals;
        }

        public string AgentTypeName { get; }
        public string ConditionTypeFQN { get; }
        public ImmutableArray<string> CtorArgLiterals { get; }
    }

    private readonly struct AgentFunctionParameterInfo
    {
        public AgentFunctionParameterInfo(
            string name, string typeFullName,
            string jsonSchemaType, string? itemJsonSchemaType,
            bool isCancellationToken, bool isNullable, bool hasDefault, string? description)
        {
            Name = name; TypeFullName = typeFullName;
            JsonSchemaType = jsonSchemaType; ItemJsonSchemaType = itemJsonSchemaType;
            IsCancellationToken = isCancellationToken; IsNullable = isNullable;
            HasDefault = hasDefault; Description = description;
        }

        public string Name { get; }
        public string TypeFullName { get; }
        public string JsonSchemaType { get; }
        public string? ItemJsonSchemaType { get; }
        public bool IsCancellationToken { get; }
        public bool IsNullable { get; }
        public bool HasDefault { get; }
        public string? Description { get; }
        public bool IsRequired => !IsCancellationToken && !IsNullable && !HasDefault;
    }

    private readonly struct AgentFunctionMethodInfo
    {
        public AgentFunctionMethodInfo(
            string methodName, bool isAsync, bool isVoidLike,
            string? returnValueTypeFQN, ImmutableArray<AgentFunctionParameterInfo> parameters,
            string description)
        {
            MethodName = methodName; IsAsync = isAsync; IsVoidLike = isVoidLike;
            ReturnValueTypeFQN = returnValueTypeFQN; Parameters = parameters; Description = description;
        }

        public string MethodName { get; }
        public bool IsAsync { get; }
        public bool IsVoidLike { get; }
        public string? ReturnValueTypeFQN { get; }
        public ImmutableArray<AgentFunctionParameterInfo> Parameters { get; }
        public string Description { get; }
    }
}