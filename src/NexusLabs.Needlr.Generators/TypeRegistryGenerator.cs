using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Incremental source generator that produces a compile-time type registry
/// for dependency injection, eliminating runtime reflection.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TypeRegistryGenerator : IIncrementalGenerator
{
    private const string GenerateTypeRegistryAttributeName = "NexusLabs.Needlr.Generators.GenerateTypeRegistryAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find assemblies marked with [GenerateTypeRegistry]
        var attributeProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateTypeRegistryAttributeName,
                predicate: static (node, _) => node is CompilationUnitSyntax,
                transform: static (ctx, _) => GetAttributeInfo(ctx))
            .Where(static info => info.HasValue)
            .Select(static (info, _) => info!.Value);

        // Combine with compilation to get access to all referenced assemblies
        var compilationAndAttributes = context.CompilationProvider
            .Combine(attributeProvider.Collect());

        // Generate the TypeRegistry source
        context.RegisterSourceOutput(compilationAndAttributes, static (spc, source) =>
        {
            var (compilation, attributes) = source;

            if (attributes.IsEmpty)
                return;

            // Use the first attribute (only one should be present per assembly)
            var attributeInfo = attributes[0];

            var discoveryResult = DiscoverTypes(
                compilation,
                attributeInfo.NamespacePrefixes,
                attributeInfo.IncludeSelf);

            var sourceText = GenerateTypeRegistrySource(discoveryResult);
            spc.AddSource("TypeRegistry.g.cs", SourceText.From(sourceText, Encoding.UTF8));
        });
    }

    private static AttributeInfo? GetAttributeInfo(GeneratorAttributeSyntaxContext context)
    {
        foreach (var attribute in context.Attributes)
        {
            if (attribute.AttributeClass?.ToDisplayString() != GenerateTypeRegistryAttributeName)
                continue;

            string[]? namespacePrefixes = null;
            var includeSelf = true;

            foreach (var namedArg in attribute.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "IncludeNamespacePrefixes":
                        if (!namedArg.Value.IsNull && namedArg.Value.Values.Length > 0)
                        {
                            namespacePrefixes = namedArg.Value.Values
                                .Where(v => v.Value is string)
                                .Select(v => (string)v.Value!)
                                .ToArray();
                        }
                        break;

                    case "IncludeSelf":
                        if (namedArg.Value.Value is bool selfValue)
                        {
                            includeSelf = selfValue;
                        }
                        break;
                }
            }

            return new AttributeInfo(namespacePrefixes, includeSelf);
        }

        return null;
    }

    private static DiscoveryResult DiscoverTypes(
        Compilation compilation,
        string[]? namespacePrefixes,
        bool includeSelf)
    {
        var injectableTypes = new List<DiscoveredType>();
        var pluginTypes = new List<DiscoveredPlugin>();
        var prefixList = namespacePrefixes?.ToList();

        // Collect types from the current compilation if includeSelf is true
        if (includeSelf)
        {
            CollectTypesFromAssembly(compilation.Assembly, prefixList, injectableTypes, pluginTypes);
        }

        // Collect types from all referenced assemblies
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                CollectTypesFromAssembly(assemblySymbol, prefixList, injectableTypes, pluginTypes);
            }
        }

        return new DiscoveryResult(injectableTypes, pluginTypes);
    }

    private static void CollectTypesFromAssembly(
        IAssemblySymbol assembly,
        IReadOnlyList<string>? namespacePrefixes,
        List<DiscoveredType> injectableTypes,
        List<DiscoveredPlugin> pluginTypes)
    {
        foreach (var typeSymbol in TypeDiscoveryHelper.GetAllTypes(assembly.GlobalNamespace))
        {
            if (!TypeDiscoveryHelper.MatchesNamespacePrefix(typeSymbol, namespacePrefixes))
                continue;

            // Check for injectable types
            if (TypeDiscoveryHelper.IsInjectableType(typeSymbol))
            {
                // Determine lifetime first - only include types that are actually injectable
                var lifetime = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);
                if (lifetime.HasValue)
                {
                    var interfaces = TypeDiscoveryHelper.GetRegisterableInterfaces(typeSymbol);
                    var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                    var interfaceNames = interfaces.Select(i => TypeDiscoveryHelper.GetFullyQualifiedName(i)).ToArray();

                    injectableTypes.Add(new DiscoveredType(typeName, interfaceNames, assembly.Name, lifetime.Value));
                }
            }

            // Check for plugin types (concrete class with parameterless ctor and interfaces)
            if (TypeDiscoveryHelper.IsPluginType(typeSymbol))
            {
                var pluginInterfaces = TypeDiscoveryHelper.GetPluginInterfaces(typeSymbol);
                if (pluginInterfaces.Count > 0)
                {
                    var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                    var interfaceNames = pluginInterfaces.Select(i => TypeDiscoveryHelper.GetFullyQualifiedName(i)).ToArray();
                    var attributeNames = TypeDiscoveryHelper.GetPluginAttributes(typeSymbol).ToArray();

                    pluginTypes.Add(new DiscoveredPlugin(typeName, interfaceNames, assembly.Name, attributeNames));
                }
            }
        }
    }

    private static string GenerateTypeRegistrySource(DiscoveryResult discoveryResult)
    {
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine("using NexusLabs.Needlr.Generators;");
        builder.AppendLine();
        builder.AppendLine("namespace NexusLabs.Needlr.Generated;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Compile-time generated registry of injectable types and plugins.");
        builder.AppendLine("/// This eliminates the need for runtime reflection-based type discovery.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine("public static class TypeRegistry");
        builder.AppendLine("{");

        GenerateInjectableTypesArray(builder, discoveryResult.InjectableTypes);
        builder.AppendLine();
        GeneratePluginTypesArray(builder, discoveryResult.PluginTypes);

        builder.AppendLine();
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Gets all injectable types discovered at compile time.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <returns>A read-only list of injectable type information.</returns>");
        builder.AppendLine("    public static IReadOnlyList<InjectableTypeInfo> GetInjectableTypes() => _types;");
        builder.AppendLine();
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Gets all plugin types discovered at compile time.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <returns>A read-only list of plugin type information.</returns>");
        builder.AppendLine("    public static IReadOnlyList<PluginTypeInfo> GetPluginTypes() => _plugins;");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void GenerateInjectableTypesArray(StringBuilder builder, IReadOnlyList<DiscoveredType> types)
    {
        builder.AppendLine("    private static readonly InjectableTypeInfo[] _types =");
        builder.AppendLine("    [");

        var typesByAssembly = types.GroupBy(t => t.AssemblyName).OrderBy(g => g.Key);

        foreach (var group in typesByAssembly)
        {
            builder.AppendLine($"        // From {group.Key}");

            foreach (var type in group.OrderBy(t => t.TypeName))
            {
                builder.Append($"        new(typeof({type.TypeName}), ");

                if (type.InterfaceNames.Length == 0)
                {
                    builder.Append("Array.Empty<Type>(), ");
                }
                else
                {
                    builder.Append("[");
                    builder.Append(string.Join(", ", type.InterfaceNames.Select(i => $"typeof({i})")));
                    builder.Append("], ");
                }

                builder.AppendLine($"InjectableLifetime.{type.Lifetime}),");
            }
        }

        builder.AppendLine("    ];");
    }

    private static void GeneratePluginTypesArray(StringBuilder builder, IReadOnlyList<DiscoveredPlugin> plugins)
    {
        builder.AppendLine("    private static readonly PluginTypeInfo[] _plugins =");
        builder.AppendLine("    [");

        var pluginsByAssembly = plugins.GroupBy(p => p.AssemblyName).OrderBy(g => g.Key);

        foreach (var group in pluginsByAssembly)
        {
            builder.AppendLine($"        // From {group.Key}");

            foreach (var plugin in group.OrderBy(p => p.TypeName))
            {
                builder.Append($"        new(typeof({plugin.TypeName}), ");

                // Interfaces
                if (plugin.InterfaceNames.Length == 0)
                {
                    builder.Append("Array.Empty<Type>(), ");
                }
                else
                {
                    builder.Append("[");
                    builder.Append(string.Join(", ", plugin.InterfaceNames.Select(i => $"typeof({i})")));
                    builder.Append("], ");
                }

                // Factory lambda - no Activator.CreateInstance needed
                builder.Append($"() => new {plugin.TypeName}(), ");

                // Attributes
                if (plugin.AttributeNames.Length == 0)
                {
                    builder.AppendLine("Array.Empty<Type>()),");
                }
                else
                {
                    builder.Append("[");
                    builder.Append(string.Join(", ", plugin.AttributeNames.Select(a => $"typeof({a})")));
                    builder.AppendLine("]),");
                }
            }
        }

        builder.AppendLine("    ];");
    }

    private readonly struct AttributeInfo
    {
        public AttributeInfo(string[]? namespacePrefixes, bool includeSelf)
        {
            NamespacePrefixes = namespacePrefixes;
            IncludeSelf = includeSelf;
        }

        public string[]? NamespacePrefixes { get; }
        public bool IncludeSelf { get; }
    }

    private readonly struct DiscoveredType
    {
        public DiscoveredType(string typeName, string[] interfaceNames, string assemblyName, GeneratorLifetime lifetime)
        {
            TypeName = typeName;
            InterfaceNames = interfaceNames;
            AssemblyName = assemblyName;
            Lifetime = lifetime;
        }

        public string TypeName { get; }
        public string[] InterfaceNames { get; }
        public string AssemblyName { get; }
        public GeneratorLifetime Lifetime { get; }
    }

    private readonly struct DiscoveredPlugin
    {
        public DiscoveredPlugin(string typeName, string[] interfaceNames, string assemblyName, string[] attributeNames)
        {
            TypeName = typeName;
            InterfaceNames = interfaceNames;
            AssemblyName = assemblyName;
            AttributeNames = attributeNames;
        }

        public string TypeName { get; }
        public string[] InterfaceNames { get; }
        public string AssemblyName { get; }
        public string[] AttributeNames { get; }
    }

    private readonly struct DiscoveryResult
    {
        public DiscoveryResult(
            IReadOnlyList<DiscoveredType> injectableTypes,
            IReadOnlyList<DiscoveredPlugin> pluginTypes)
        {
            InjectableTypes = injectableTypes;
            PluginTypes = pluginTypes;
        }

        public IReadOnlyList<DiscoveredType> InjectableTypes { get; }
        public IReadOnlyList<DiscoveredPlugin> PluginTypes { get; }
    }
}
