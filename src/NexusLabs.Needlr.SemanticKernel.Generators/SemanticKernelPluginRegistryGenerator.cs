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

namespace NexusLabs.Needlr.SemanticKernel.Generators;

/// <summary>
/// Source generator for Semantic Kernel plugins.
/// Discovers classes with [KernelFunction] methods and generates registration code.
/// </summary>
[Generator]
public class SemanticKernelPluginRegistryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all class declarations
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsClassDeclaration(s),
                transform: static (ctx, ct) => GetKernelPluginInfo(ctx, ct))
            .Where(static m => m is not null);

        // Combine with compilation
        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        // Generate the source
        context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Left, source.Right!, spc));
    }

    private static bool IsClassDeclaration(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax;
    }

    private static KernelPluginInfo? GetKernelPluginInfo(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) as INamedTypeSymbol;

        if (typeSymbol is null)
            return null;

        return TryGetKernelPluginInfo(typeSymbol);
    }

    private static KernelPluginInfo? TryGetKernelPluginInfo(INamedTypeSymbol typeSymbol)
    {
        const string KernelFunctionAttributeName = "Microsoft.SemanticKernel.KernelFunctionAttribute";

        // Must be a class (static or instance)
        if (typeSymbol.TypeKind != TypeKind.Class)
            return null;

        // Must be accessible from generated code
        if (!IsAccessibleFromGeneratedCode(typeSymbol))
            return null;

        // For non-static classes, must not be abstract
        if (!typeSymbol.IsStatic && typeSymbol.IsAbstract)
            return null;

        // Check for methods with [KernelFunction] attribute
        bool hasKernelFunction = false;
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
                var attrName = attribute.AttributeClass?.ToDisplayString();
                if (attrName == KernelFunctionAttributeName)
                {
                    hasKernelFunction = true;
                    break;
                }
            }

            if (hasKernelFunction)
                break;
        }

        if (!hasKernelFunction)
            return null;

        var typeName = GetFullyQualifiedName(typeSymbol);
        var assemblyName = typeSymbol.ContainingAssembly?.Name ?? "Unknown";
        return new KernelPluginInfo(typeName, assemblyName, typeSymbol.IsStatic);
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

    private static string GetFullyQualifiedName(ITypeSymbol typeSymbol)
    {
        return "global::" + typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
    }

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

    private static void Execute(Compilation compilation, ImmutableArray<KernelPluginInfo?> plugins, SourceProductionContext context)
    {
        var validPlugins = plugins.Where(p => p.HasValue).Select(p => p!.Value).ToList();

        if (validPlugins.Count == 0)
            return;

        var assemblyName = compilation.AssemblyName ?? "UnknownAssembly";
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        var source = GenerateKernelPluginsSource(validPlugins, safeAssemblyName);
        context.AddSource("SemanticKernelPlugins.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateKernelPluginsSource(List<KernelPluginInfo> plugins, string safeAssemblyName)
    {
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("// Needlr SemanticKernel Plugins");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Generated registry for Semantic Kernel plugin types discovered at compile time.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.SemanticKernel.Generators\", \"1.0.0\")]");
        builder.AppendLine("public static class KernelPluginRegistry");
        builder.AppendLine("{");

        // Generate static entries array
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// All discovered kernel plugin types.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    public static IReadOnlyList<(Type PluginType, bool IsStatic)> Entries { get; } = new (Type, bool)[]");
        builder.AppendLine("    {");

        foreach (var plugin in plugins)
        {
            builder.AppendLine($"        (typeof({plugin.TypeName}), {plugin.IsStatic.ToString().ToLowerInvariant()}),");
        }

        builder.AppendLine("    };");
        builder.AppendLine();

        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Gets the number of plugin types discovered at compile time.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine($"    public static int Count => {plugins.Count};");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private readonly struct KernelPluginInfo
    {
        public KernelPluginInfo(string typeName, string assemblyName, bool isStatic)
        {
            TypeName = typeName;
            AssemblyName = assemblyName;
            IsStatic = isStatic;
        }

        public string TypeName { get; }
        public string AssemblyName { get; }
        public bool IsStatic { get; }
    }
}
