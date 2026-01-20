using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using System.Text;

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
            var assemblyName = compilation.AssemblyName ?? "Generated";

            var discoveryResult = DiscoverTypes(
                compilation,
                attributeInfo.NamespacePrefixes,
                attributeInfo.IncludeSelf);

            // Report errors for inaccessible internal types in referenced assemblies
            foreach (var inaccessibleType in discoveryResult.InaccessibleTypes)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InaccessibleInternalType,
                    Location.None,
                    inaccessibleType.TypeName,
                    inaccessibleType.AssemblyName));
            }

            // Report errors for referenced assemblies with internal plugin types but no [GenerateTypeRegistry]
            foreach (var missingPlugin in discoveryResult.MissingTypeRegistryPlugins)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MissingGenerateTypeRegistryAttribute,
                    Location.None,
                    missingPlugin.AssemblyName,
                    missingPlugin.TypeName));
            }

            var sourceText = GenerateTypeRegistrySource(discoveryResult, assemblyName);
            spc.AddSource("TypeRegistry.g.cs", SourceText.From(sourceText, Encoding.UTF8));

            var bootstrapText = GenerateModuleInitializerBootstrapSource(assemblyName);
            spc.AddSource("NeedlrSourceGenBootstrap.g.cs", SourceText.From(bootstrapText, Encoding.UTF8));

            // Generate SignalR hub registrations if any were discovered
            if (discoveryResult.HubRegistrations.Count > 0)
            {
                var hubRegistrationsText = GenerateSignalRHubRegistrationsSource(discoveryResult.HubRegistrations, assemblyName);
                spc.AddSource("SignalRHubRegistrations.g.cs", SourceText.From(hubRegistrationsText, Encoding.UTF8));
            }

            // Generate SemanticKernel plugin type registry if any were discovered
            if (discoveryResult.KernelPlugins.Count > 0)
            {
                var kernelPluginsText = GenerateSemanticKernelPluginsSource(discoveryResult.KernelPlugins, assemblyName);
                spc.AddSource("SemanticKernelPlugins.g.cs", SourceText.From(kernelPluginsText, Encoding.UTF8));
            }
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
        var hubRegistrations = new List<DiscoveredHubRegistration>();
        var kernelPlugins = new List<DiscoveredKernelPlugin>();
        var inaccessibleTypes = new List<InaccessibleType>();
        var prefixList = namespacePrefixes?.ToList();

        // Collect types from the current compilation if includeSelf is true
        if (includeSelf)
        {
            CollectTypesFromAssembly(compilation.Assembly, prefixList, injectableTypes, pluginTypes, hubRegistrations, kernelPlugins, inaccessibleTypes, compilation, isCurrentAssembly: true);
        }

        // Collect types from all referenced assemblies
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                CollectTypesFromAssembly(assemblySymbol, prefixList, injectableTypes, pluginTypes, hubRegistrations, kernelPlugins, inaccessibleTypes, compilation, isCurrentAssembly: false);
            }
        }

        // Check for referenced assemblies with internal plugin types but no [GenerateTypeRegistry]
        var missingTypeRegistryPlugins = new List<MissingTypeRegistryPlugin>();
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                // Skip assemblies that already have [GenerateTypeRegistry]
                if (TypeDiscoveryHelper.HasGenerateTypeRegistryAttribute(assemblySymbol))
                    continue;

                // Look for internal types that implement Needlr plugin interfaces
                foreach (var typeSymbol in TypeDiscoveryHelper.GetAllTypes(assemblySymbol.GlobalNamespace))
                {
                    if (!TypeDiscoveryHelper.IsInternalOrLessAccessible(typeSymbol))
                        continue;

                    if (!TypeDiscoveryHelper.ImplementsNeedlrPluginInterface(typeSymbol))
                        continue;

                    // This is an internal plugin type in an assembly without [GenerateTypeRegistry]
                    var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                    missingTypeRegistryPlugins.Add(new MissingTypeRegistryPlugin(typeName, assemblySymbol.Name));
                }
            }
        }

        return new DiscoveryResult(injectableTypes, pluginTypes, hubRegistrations, kernelPlugins, inaccessibleTypes, missingTypeRegistryPlugins);
    }

    private static void CollectTypesFromAssembly(
        IAssemblySymbol assembly,
        IReadOnlyList<string>? namespacePrefixes,
        List<DiscoveredType> injectableTypes,
        List<DiscoveredPlugin> pluginTypes,
        List<DiscoveredHubRegistration> hubRegistrations,
        List<DiscoveredKernelPlugin> kernelPlugins,
        List<InaccessibleType> inaccessibleTypes,
        Compilation compilation,
        bool isCurrentAssembly)
    {
        foreach (var typeSymbol in TypeDiscoveryHelper.GetAllTypes(assembly.GlobalNamespace))
        {
            if (!TypeDiscoveryHelper.MatchesNamespacePrefix(typeSymbol, namespacePrefixes))
                continue;

            // For referenced assemblies, check if the type would be registerable but is inaccessible
            if (!isCurrentAssembly && TypeDiscoveryHelper.IsInternalOrLessAccessible(typeSymbol))
            {
                // Check if this type would have been registered if it were accessible
                if (TypeDiscoveryHelper.WouldBeInjectableIgnoringAccessibility(typeSymbol) ||
                    TypeDiscoveryHelper.WouldBePluginIgnoringAccessibility(typeSymbol))
                {
                    var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                    inaccessibleTypes.Add(new InaccessibleType(typeName, assembly.Name));
                }
                continue; // Skip further processing for inaccessible types
            }

            // Check for injectable types
            if (TypeDiscoveryHelper.IsInjectableType(typeSymbol, isCurrentAssembly))
            {
                // Determine lifetime first - only include types that are actually injectable
                var lifetime = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);
                if (lifetime.HasValue)
                {
                    var interfaces = TypeDiscoveryHelper.GetRegisterableInterfaces(typeSymbol);
                    var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                    var interfaceNames = interfaces.Select(i => TypeDiscoveryHelper.GetFullyQualifiedName(i)).ToArray();
                    var constructorParams = TypeDiscoveryHelper.GetBestConstructorParameters(typeSymbol) ?? [];

                    injectableTypes.Add(new DiscoveredType(typeName, interfaceNames, assembly.Name, lifetime.Value, constructorParams.ToArray()));
                }
            }

            // Check for plugin types (concrete class with parameterless ctor and interfaces)
            if (TypeDiscoveryHelper.IsPluginType(typeSymbol, isCurrentAssembly))
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

            // Check for IHubRegistrationPlugin implementations
            var hubInfo = TypeDiscoveryHelper.TryGetHubRegistrationInfo(typeSymbol, compilation, isCurrentAssembly);
            if (hubInfo.HasValue)
            {
                hubRegistrations.Add(new DiscoveredHubRegistration(
                    TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol),
                    hubInfo.Value.HubTypeName,
                    hubInfo.Value.HubPath));
            }

            // Check for SemanticKernel plugin types (classes/statics with [KernelFunction] methods)
            if (TypeDiscoveryHelper.HasKernelFunctions(typeSymbol, isCurrentAssembly))
            {
                var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                var isStatic = typeSymbol.IsStatic;
                kernelPlugins.Add(new DiscoveredKernelPlugin(typeName, assembly.Name, isStatic));
            }
        }
    }

    private static string GenerateTypeRegistrySource(DiscoveryResult discoveryResult, string assemblyName)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        builder.AppendLine();
        builder.AppendLine("using NexusLabs.Needlr.Generators;");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
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

    private static string GenerateModuleInitializerBootstrapSource(string assemblyName)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();
        builder.AppendLine("internal static class NeedlrSourceGenModuleInitializer");
        builder.AppendLine("{");
        builder.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        builder.AppendLine("    internal static void Initialize()");
        builder.AppendLine("    {");
        builder.AppendLine("        global::NexusLabs.Needlr.Generators.NeedlrSourceGenBootstrap.Register(");
        builder.AppendLine($"            global::{safeAssemblyName}.Generated.TypeRegistry.GetInjectableTypes,");
        builder.AppendLine($"            global::{safeAssemblyName}.Generated.TypeRegistry.GetPluginTypes);");
        builder.AppendLine("    }");
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

                // Interfaces
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

                // Lifetime
                builder.Append($"InjectableLifetime.{type.Lifetime}, ");

                // Factory lambda - resolves dependencies and creates instance without reflection
                builder.Append("sp => new ");
                builder.Append(type.TypeName);
                builder.Append("(");
                if (type.ConstructorParameterTypes.Length > 0)
                {
                    var parameterExpressions = type.ConstructorParameterTypes
                        .Select(p => $"sp.GetRequiredService<{p}>()");
                    builder.Append(string.Join(", ", parameterExpressions));
                }
                builder.AppendLine(")),");
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

    private static string GenerateSignalRHubRegistrationsSource(IReadOnlyList<DiscoveredHubRegistration> hubRegistrations, string assemblyName)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.AspNetCore.Builder;");
        builder.AppendLine("using Microsoft.AspNetCore.SignalR;");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Compile-time generated SignalR hub registrations.");
        builder.AppendLine("/// This eliminates the need for runtime reflection to call MapHub&lt;T&gt;().");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine("public static class SignalRHubRegistrations");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Registers all discovered SignalR hubs with the web application.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <param name=\"app\">The web application to configure.</param>");
        builder.AppendLine("    /// <returns>The web application for chaining.</returns>");
        builder.AppendLine("    public static WebApplication MapGeneratedHubs(this WebApplication app)");
        builder.AppendLine("    {");

        foreach (var hub in hubRegistrations)
        {
            builder.AppendLine($"        // From {hub.PluginTypeName}");
            builder.AppendLine($"        app.MapHub<{hub.HubTypeName}>(\"{hub.HubPath}\");");
        }

        builder.AppendLine("        return app;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Gets the number of hub registrations discovered at compile time.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine($"    public static int Count => {hubRegistrations.Count};");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string GenerateSemanticKernelPluginsSource(IReadOnlyList<DiscoveredKernelPlugin> kernelPlugins, string assemblyName)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Compile-time generated registry of SemanticKernel plugin types.");
        builder.AppendLine("/// This eliminates the need for runtime reflection to discover [KernelFunction] methods.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine("public static class SemanticKernelPlugins");
        builder.AppendLine("{");

        // Generate static type array
        var staticPlugins = kernelPlugins.Where(p => p.IsStatic).ToList();
        var instancePlugins = kernelPlugins.Where(p => !p.IsStatic).ToList();

        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Gets all static types with [KernelFunction] methods discovered at compile time.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    public static IReadOnlyList<Type> StaticPluginTypes { get; } = new Type[]");
        builder.AppendLine("    {");
        foreach (var plugin in staticPlugins)
        {
            builder.AppendLine($"        typeof({plugin.TypeName}), // From {plugin.AssemblyName}");
        }
        builder.AppendLine("    };");
        builder.AppendLine();

        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Gets all instance types with [KernelFunction] methods discovered at compile time.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    public static IReadOnlyList<Type> InstancePluginTypes { get; } = new Type[]");
        builder.AppendLine("    {");
        foreach (var plugin in instancePlugins)
        {
            builder.AppendLine($"        typeof({plugin.TypeName}), // From {plugin.AssemblyName}");
        }
        builder.AppendLine("    };");
        builder.AppendLine();

        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Gets all types with [KernelFunction] methods discovered at compile time.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    public static IReadOnlyList<Type> AllPluginTypes { get; } = new Type[]");
        builder.AppendLine("    {");
        foreach (var plugin in kernelPlugins)
        {
            builder.AppendLine($"        typeof({plugin.TypeName}), // From {plugin.AssemblyName}");
        }
        builder.AppendLine("    };");
        builder.AppendLine();

        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Gets the number of plugin types discovered at compile time.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine($"    public static int Count => {kernelPlugins.Count};");
        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Sanitizes an assembly name to be a valid C# identifier for use in namespaces.
    /// </summary>
    private static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Generated";

        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
            }
            else if (c == '.' || c == '-' || c == ' ')
            {
                // Keep dots for namespace segments, replace dashes/spaces with underscores
                sb.Append(c == '.' ? '.' : '_');
            }
            // Skip other characters
        }

        var result = sb.ToString();

        // Ensure each segment doesn't start with a digit
        var segments = result.Split('.');
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length > 0 && char.IsDigit(segments[i][0]))
            {
                segments[i] = "_" + segments[i];
            }
        }

        return string.Join(".", segments.Where(s => s.Length > 0));
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
        public DiscoveredType(string typeName, string[] interfaceNames, string assemblyName, GeneratorLifetime lifetime, string[] constructorParameterTypes)
        {
            TypeName = typeName;
            InterfaceNames = interfaceNames;
            AssemblyName = assemblyName;
            Lifetime = lifetime;
            ConstructorParameterTypes = constructorParameterTypes;
        }

        public string TypeName { get; }
        public string[] InterfaceNames { get; }
        public string AssemblyName { get; }
        public GeneratorLifetime Lifetime { get; }
        public string[] ConstructorParameterTypes { get; }
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

    private readonly struct DiscoveredHubRegistration
    {
        public DiscoveredHubRegistration(string pluginTypeName, string hubTypeName, string hubPath)
        {
            PluginTypeName = pluginTypeName;
            HubTypeName = hubTypeName;
            HubPath = hubPath;
        }

        public string PluginTypeName { get; }
        public string HubTypeName { get; }
        public string HubPath { get; }
    }

    private readonly struct DiscoveredKernelPlugin
    {
        public DiscoveredKernelPlugin(string typeName, string assemblyName, bool isStatic)
        {
            TypeName = typeName;
            AssemblyName = assemblyName;
            IsStatic = isStatic;
        }

        public string TypeName { get; }
        public string AssemblyName { get; }
        public bool IsStatic { get; }
    }

    private readonly struct InaccessibleType
    {
        public InaccessibleType(string typeName, string assemblyName)
        {
            TypeName = typeName;
            AssemblyName = assemblyName;
        }

        public string TypeName { get; }
        public string AssemblyName { get; }
    }

    private readonly struct MissingTypeRegistryPlugin
    {
        public MissingTypeRegistryPlugin(string typeName, string assemblyName)
        {
            TypeName = typeName;
            AssemblyName = assemblyName;
        }

        public string TypeName { get; }
        public string AssemblyName { get; }
    }

    private readonly struct DiscoveryResult
    {
        public DiscoveryResult(
            IReadOnlyList<DiscoveredType> injectableTypes,
            IReadOnlyList<DiscoveredPlugin> pluginTypes,
            IReadOnlyList<DiscoveredHubRegistration> hubRegistrations,
            IReadOnlyList<DiscoveredKernelPlugin> kernelPlugins,
            IReadOnlyList<InaccessibleType> inaccessibleTypes,
            IReadOnlyList<MissingTypeRegistryPlugin> missingTypeRegistryPlugins)
        {
            InjectableTypes = injectableTypes;
            PluginTypes = pluginTypes;
            HubRegistrations = hubRegistrations;
            KernelPlugins = kernelPlugins;
            InaccessibleTypes = inaccessibleTypes;
            MissingTypeRegistryPlugins = missingTypeRegistryPlugins;
        }

        public IReadOnlyList<DiscoveredType> InjectableTypes { get; }
        public IReadOnlyList<DiscoveredPlugin> PluginTypes { get; }
        public IReadOnlyList<DiscoveredHubRegistration> HubRegistrations { get; }
        public IReadOnlyList<DiscoveredKernelPlugin> KernelPlugins { get; }
        public IReadOnlyList<InaccessibleType> InaccessibleTypes { get; }
        public IReadOnlyList<MissingTypeRegistryPlugin> MissingTypeRegistryPlugins { get; }
    }
}
