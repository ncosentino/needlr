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

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pipeline A: [AgentFunction] method-bearing classes → AgentFrameworkFunctionRegistry
        var functionClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetAgentFunctionTypeInfo(ctx, ct))
            .Where(static m => m is not null);

        // Pipeline B: [AgentFunctionGroup] class-level annotations → AgentFrameworkFunctionGroupRegistry
        var groupClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetAgentFunctionGroupEntries(ctx, ct))
            .Where(static arr => arr.Length > 0);

        // Pipeline C: [NeedlrAiAgent] declared agent types → AgentRegistry + partial companions
        var agentClasses = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                NeedlrAiAgentAttributeName,
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetNeedlrAiAgentTypeInfo(ctx, ct))
            .Where(static m => m is not null);

        // Unified output: all three pipelines combined with compilation metadata.
        // Always emits all registries + [ModuleInitializer] bootstrap, even when empty.
        var combined = functionClasses.Collect()
            .Combine(groupClasses.Collect())
            .Combine(agentClasses.Collect())
            .Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined,
            static (spc, data) =>
            {
                var (((functionData, groupData), agentData), compilation) = data;
                ExecuteAll(functionData, groupData, agentData, compilation, spc);
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

        return new AgentFunctionTypeInfo(
            GetFullyQualifiedName(typeSymbol),
            typeSymbol.ContainingAssembly?.Name ?? "Unknown");
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

        return new NeedlrAiAgentTypeInfo(
            GetFullyQualifiedName(typeSymbol),
            typeSymbol.Name,
            namespaceName,
            isPartial);
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
        Compilation compilation,
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

        // Always emit all registries (may be empty) and the bootstrap
        spc.AddSource("AgentFrameworkFunctions.g.cs",
            SourceText.From(GenerateRegistrySource(validFunctionTypes, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentFrameworkFunctionGroups.g.cs",
            SourceText.From(GenerateGroupRegistrySource(groupedByName, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("AgentRegistry.g.cs",
            SourceText.From(GenerateAgentRegistrySource(validAgentTypes, safeAssemblyName), Encoding.UTF8));

        spc.AddSource("NeedlrAgentFrameworkBootstrap.g.cs",
            SourceText.From(GenerateBootstrapSource(safeAssemblyName), Encoding.UTF8));

        // Partial companions for [NeedlrAiAgent] classes declared as partial
        foreach (var agentType in validAgentTypes.Where(a => a.IsPartial))
        {
            var safeTypeName = agentType.TypeName
                .Replace("global::", "")
                .Replace(".", "_")
                .Replace("<", "_")
                .Replace(">", "_");

            spc.AddSource($"{safeTypeName}.NeedlrAiAgent.g.cs",
                SourceText.From(GeneratePartialCompanionSource(agentType), Encoding.UTF8));
        }
    }

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
            () => AgentRegistry.AllAgentTypes);
    }}
}}
";
    }

    private static string GeneratePartialCompanionSource(NeedlrAiAgentTypeInfo agentType)
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
        sb.AppendLine($"    public static string AgentName => nameof({agentType.ClassName});");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private readonly struct AgentFunctionTypeInfo
    {
        public AgentFunctionTypeInfo(string typeName, string assemblyName)
        {
            TypeName = typeName;
            AssemblyName = assemblyName;
        }

        public string TypeName { get; }
        public string AssemblyName { get; }
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
        public NeedlrAiAgentTypeInfo(string typeName, string className, string? namespaceName, bool isPartial)
        {
            TypeName = typeName;
            ClassName = className;
            NamespaceName = namespaceName;
            IsPartial = isPartial;
        }

        public string TypeName { get; }
        public string ClassName { get; }
        public string? NamespaceName { get; }
        public bool IsPartial { get; }
    }
}