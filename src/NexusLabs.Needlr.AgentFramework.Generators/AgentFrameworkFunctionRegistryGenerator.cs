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
/// </summary>
[Generator]
public class AgentFrameworkFunctionRegistryGenerator : IIncrementalGenerator
{
    private const string AgentFunctionAttributeName = "NexusLabs.Needlr.AgentFramework.AgentFunctionAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetAgentFunctionTypeInfo(ctx, ct))
            .Where(static m => m is not null);

        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses,
            static (spc, source) => Execute(source.Left, source.Right!, spc));
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

    private static void Execute(
        Compilation compilation,
        ImmutableArray<AgentFunctionTypeInfo?> types,
        SourceProductionContext context)
    {
        var validTypes = types
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .ToList();

        if (validTypes.Count == 0)
            return;

        var assemblyName = compilation.AssemblyName ?? "UnknownAssembly";
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        var source = GenerateRegistrySource(validTypes, safeAssemblyName);
        context.AddSource("AgentFrameworkFunctions.g.cs", SourceText.From(source, Encoding.UTF8));
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
}
