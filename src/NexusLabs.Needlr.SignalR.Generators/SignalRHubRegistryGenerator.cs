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

namespace NexusLabs.Needlr.SignalR.Generators;

/// <summary>
/// Source generator for SignalR hub registrations.
/// Discovers IHubRegistrationPlugin implementations and generates registration code.
/// </summary>
[Generator]
public class SignalRHubRegistryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all class declarations
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsClassDeclaration(s),
                transform: static (ctx, ct) => GetHubRegistrationInfo(ctx, ct))
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

    private static HubRegistrationInfo? GetHubRegistrationInfo(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) as INamedTypeSymbol;

        if (typeSymbol is null)
            return null;

        return TryGetHubRegistrationInfo(typeSymbol, semanticModel.Compilation);
    }

    private static HubRegistrationInfo? TryGetHubRegistrationInfo(INamedTypeSymbol typeSymbol, Compilation compilation)
    {
        const string HubRegistrationPluginInterfaceName = "NexusLabs.Needlr.SignalR.IHubRegistrationPlugin";

        // Check if type implements IHubRegistrationPlugin
        if (!ImplementsInterface(typeSymbol, HubRegistrationPluginInterfaceName))
            return null;

        // Must be accessible from generated code
        if (!IsAccessibleFromGeneratedCode(typeSymbol))
            return null;

        // Must have a parameterless constructor to be instantiable
        if (!HasParameterlessConstructor(typeSymbol))
            return null;

        // Try to find HubPath and HubType property values
        string? hubPath = null;
        string? hubTypeName = null;

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol property)
                continue;

            if (property.Name == "HubPath")
            {
                hubPath = TryGetPropertyStringValue(property, compilation);
            }
            else if (property.Name == "HubType")
            {
                hubTypeName = TryGetPropertyTypeValue(property, compilation);
            }
        }

        if (hubPath != null && hubTypeName != null)
        {
            var pluginTypeName = GetFullyQualifiedName(typeSymbol);
            return new HubRegistrationInfo(pluginTypeName, hubTypeName, hubPath);
        }

        return null;
    }

    private static bool ImplementsInterface(INamedTypeSymbol typeSymbol, string interfaceFullName)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.ToDisplayString() == interfaceFullName)
                return true;
        }
        return false;
    }

    private static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol typeSymbol)
    {
        // Check accessibility - must be public or internal
        if (typeSymbol.DeclaredAccessibility == Accessibility.Private ||
            typeSymbol.DeclaredAccessibility == Accessibility.Protected)
            return false;

        // Check containing types
        var current = typeSymbol.ContainingType;
        while (current != null)
        {
            if (current.DeclaredAccessibility == Accessibility.Private)
                return false;
            current = current.ContainingType;
        }

        return true;
    }

    private static bool HasParameterlessConstructor(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.IsAbstract || typeSymbol.IsStatic)
            return false;

        foreach (var ctor in typeSymbol.InstanceConstructors)
        {
            if (ctor.Parameters.Length == 0 &&
                ctor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            {
                return true;
            }
        }

        // If no constructors are defined, there's an implicit parameterless constructor
        return typeSymbol.InstanceConstructors.Length == 0 ||
               typeSymbol.InstanceConstructors.All(c => c.IsImplicitlyDeclared);
    }

    private static string? TryGetPropertyStringValue(IPropertySymbol property, Compilation compilation)
    {
        var syntaxRefs = property.DeclaringSyntaxReferences;
        if (syntaxRefs.Length == 0)
            return null;

        var syntax = syntaxRefs[0].GetSyntax();

        foreach (var node in syntax.DescendantNodes())
        {
            if (node is LiteralExpressionSyntax literal)
            {
                var semanticModel = compilation.GetSemanticModel(literal.SyntaxTree);
                var constantValue = semanticModel.GetConstantValue(literal);
                if (constantValue.HasValue && constantValue.Value is string strValue)
                {
                    return strValue;
                }
            }
        }

        return null;
    }

    private static string? TryGetPropertyTypeValue(IPropertySymbol property, Compilation compilation)
    {
        var syntaxRefs = property.DeclaringSyntaxReferences;
        if (syntaxRefs.Length == 0)
            return null;

        var syntax = syntaxRefs[0].GetSyntax();

        foreach (var node in syntax.DescendantNodes())
        {
            if (node is TypeOfExpressionSyntax typeOfExpr)
            {
                var semanticModel = compilation.GetSemanticModel(typeOfExpr.SyntaxTree);
                var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
                if (typeInfo.Type is INamedTypeSymbol namedType)
                {
                    return GetFullyQualifiedName(namedType);
                }
            }
        }

        return null;
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

    private static void Execute(Compilation compilation, ImmutableArray<HubRegistrationInfo?> registrations, SourceProductionContext context)
    {
        var validRegistrations = registrations.Where(r => r.HasValue).Select(r => r!.Value).ToList();

        if (validRegistrations.Count == 0)
            return;

        var assemblyName = compilation.AssemblyName ?? "UnknownAssembly";
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        var source = GenerateHubRegistrationsSource(validRegistrations, safeAssemblyName);
        context.AddSource("SignalRHubRegistrations.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateHubRegistrationsSource(List<HubRegistrationInfo> registrations, string safeAssemblyName)
    {
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("// Needlr SignalR Hub Registrations");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Generated registry for SignalR hub registrations discovered at compile time.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.SignalR.Generators\", \"1.0.0\")]");
        builder.AppendLine("public static class SignalRHubRegistry");
        builder.AppendLine("{");

        // Generate static entries array
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// All discovered hub registration entries.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    public static IReadOnlyList<(Type PluginType, Type HubType, string Path)> Entries { get; } = new (Type, Type, string)[]");
        builder.AppendLine("    {");

        foreach (var reg in registrations)
        {
            builder.AppendLine($"        (typeof({reg.PluginTypeName}), typeof({reg.HubTypeName}), \"{EscapeStringLiteral(reg.HubPath)}\"),");
        }

        builder.AppendLine("    };");
        builder.AppendLine();

        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Gets the number of hub registrations discovered at compile time.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine($"    public static int Count => {registrations.Count};");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string EscapeStringLiteral(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private readonly struct HubRegistrationInfo
    {
        public HubRegistrationInfo(string pluginTypeName, string hubTypeName, string hubPath)
        {
            PluginTypeName = pluginTypeName;
            HubTypeName = hubTypeName;
            HubPath = hubPath;
        }

        public string PluginTypeName { get; }
        public string HubTypeName { get; }
        public string HubPath { get; }
    }
}
