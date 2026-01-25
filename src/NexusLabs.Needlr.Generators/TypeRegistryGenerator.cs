using Microsoft.CodeAnalysis;
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
        // Combine compilation with analyzer config options to read MSBuild properties
        var compilationAndOptions = context.CompilationProvider
            .Combine(context.AnalyzerConfigOptionsProvider);

        // ForAttributeWithMetadataName doesn't work for assembly-level attributes.
        // Instead, we register directly on the compilation provider and check
        // compilation.Assembly.GetAttributes() for [GenerateTypeRegistry].
        context.RegisterSourceOutput(compilationAndOptions, static (spc, source) =>
        {
            var (compilation, configOptions) = source;
            
            var attributeInfo = GetAttributeInfoFromCompilation(compilation);
            if (attributeInfo == null)
                return;

            var info = attributeInfo.Value;
            var assemblyName = compilation.AssemblyName ?? "Generated";
            
            // Read breadcrumb level from MSBuild property
            var breadcrumbLevel = GetBreadcrumbLevel(configOptions);
            var projectDirectory = GetProjectDirectory(configOptions);
            var breadcrumbs = new BreadcrumbWriter(breadcrumbLevel);

            var discoveryResult = DiscoverTypes(
                compilation,
                info.NamespacePrefixes,
                info.IncludeSelf);

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

            var sourceText = GenerateTypeRegistrySource(discoveryResult, assemblyName, breadcrumbs, projectDirectory);
            spc.AddSource("TypeRegistry.g.cs", SourceText.From(sourceText, Encoding.UTF8));

            // Discover referenced assemblies with [GenerateTypeRegistry] for forced loading
            // Note: Order of force-loading doesn't matter; ordering is applied at service registration time
            var referencedAssemblies = DiscoverReferencedAssembliesWithTypeRegistry(compilation)
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var bootstrapText = GenerateModuleInitializerBootstrapSource(assemblyName, referencedAssemblies, breadcrumbs);
            spc.AddSource("NeedlrSourceGenBootstrap.g.cs", SourceText.From(bootstrapText, Encoding.UTF8));

            // Generate SignalR hub registrations if any were discovered
            if (discoveryResult.HubRegistrations.Count > 0)
            {
                var hubRegistrationsText = GenerateSignalRHubRegistrationsSource(discoveryResult.HubRegistrations, assemblyName, breadcrumbs);
                spc.AddSource("SignalRHubRegistrations.g.cs", SourceText.From(hubRegistrationsText, Encoding.UTF8));
            }

            // Generate SemanticKernel plugin type registry if any were discovered
            if (discoveryResult.KernelPlugins.Count > 0)
            {
                var kernelPluginsText = GenerateSemanticKernelPluginsSource(discoveryResult.KernelPlugins, assemblyName, breadcrumbs);
                spc.AddSource("SemanticKernelPlugins.g.cs", SourceText.From(kernelPluginsText, Encoding.UTF8));
            }

            // Generate interceptor proxy classes if any were discovered
            if (discoveryResult.InterceptedServices.Count > 0)
            {
                var interceptorProxiesText = GenerateInterceptorProxiesSource(discoveryResult.InterceptedServices, assemblyName, breadcrumbs, projectDirectory);
                spc.AddSource("InterceptorProxies.g.cs", SourceText.From(interceptorProxiesText, Encoding.UTF8));
            }

            // Generate diagnostic output files if configured
            var diagnosticOptions = GetDiagnosticOptions(configOptions);
            if (diagnosticOptions.Enabled)
            {
                var diagnosticsText = GenerateDiagnosticsSource(discoveryResult, assemblyName, projectDirectory, diagnosticOptions);
                spc.AddSource("NeedlrDiagnostics.g.cs", SourceText.From(diagnosticsText, Encoding.UTF8));
            }
        });
    }
    
    private static BreadcrumbLevel GetBreadcrumbLevel(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider configOptions)
    {
        if (configOptions.GlobalOptions.TryGetValue("build_property.NeedlrBreadcrumbLevel", out var levelStr) &&
            !string.IsNullOrWhiteSpace(levelStr))
        {
            if (levelStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                return BreadcrumbLevel.None;
            if (levelStr.Equals("Verbose", StringComparison.OrdinalIgnoreCase))
                return BreadcrumbLevel.Verbose;
        }
        
        // Default to Minimal
        return BreadcrumbLevel.Minimal;
    }
    
    private static string? GetProjectDirectory(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider configOptions)
    {
        // Try to get the project directory from MSBuild properties
        if (configOptions.GlobalOptions.TryGetValue("build_property.ProjectDir", out var projectDir) &&
            !string.IsNullOrWhiteSpace(projectDir))
        {
            return projectDir.TrimEnd('/', '\\');
        }
        
        return null;
    }

    private static DiagnosticOptions GetDiagnosticOptions(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider configOptions)
    {
        configOptions.GlobalOptions.TryGetValue("build_property.NeedlrDiagnostics", out var enabled);
        configOptions.GlobalOptions.TryGetValue("build_property.NeedlrDiagnosticsPath", out var outputPath);
        configOptions.GlobalOptions.TryGetValue("build_property.NeedlrDiagnosticsFilter", out var filter);
        
        return DiagnosticOptions.Parse(enabled, outputPath, filter);
    }

    private static AttributeInfo? GetAttributeInfoFromCompilation(Compilation compilation)
    {
        // Get assembly-level attributes directly from the compilation
        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            var attrClassName = attribute.AttributeClass?.ToDisplayString();
            
            // Check if this is our attribute (various name format possibilities)
            if (attrClassName != GenerateTypeRegistryAttributeName)
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
        var decorators = new List<DiscoveredDecorator>();
        var interceptedServices = new List<DiscoveredInterceptedService>();
        var inaccessibleTypes = new List<InaccessibleType>();
        var prefixList = namespacePrefixes?.ToList();

        // Collect types from the current compilation if includeSelf is true
        if (includeSelf)
        {
            CollectTypesFromAssembly(compilation.Assembly, prefixList, injectableTypes, pluginTypes, hubRegistrations, kernelPlugins, decorators, interceptedServices, inaccessibleTypes, compilation, isCurrentAssembly: true);
        }

        // Collect types from all referenced assemblies
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                CollectTypesFromAssembly(assemblySymbol, prefixList, injectableTypes, pluginTypes, hubRegistrations, kernelPlugins, decorators, interceptedServices, inaccessibleTypes, compilation, isCurrentAssembly: false);
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

        return new DiscoveryResult(injectableTypes, pluginTypes, hubRegistrations, kernelPlugins, decorators, inaccessibleTypes, missingTypeRegistryPlugins, interceptedServices);
    }

    private static void CollectTypesFromAssembly(
        IAssemblySymbol assembly,
        IReadOnlyList<string>? namespacePrefixes,
        List<DiscoveredType> injectableTypes,
        List<DiscoveredPlugin> pluginTypes,
        List<DiscoveredHubRegistration> hubRegistrations,
        List<DiscoveredKernelPlugin> kernelPlugins,
        List<DiscoveredDecorator> decorators,
        List<DiscoveredInterceptedService> interceptedServices,
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

            // Check for DecoratorFor<T> attributes
            var decoratorInfos = TypeDiscoveryHelper.GetDecoratorForAttributes(typeSymbol);
            foreach (var decoratorInfo in decoratorInfos)
            {
                var sourceFilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;
                decorators.Add(new DiscoveredDecorator(
                    decoratorInfo.DecoratorTypeName,
                    decoratorInfo.ServiceTypeName,
                    decoratorInfo.Order,
                    assembly.Name,
                    sourceFilePath));
            }

            // Check for Intercept attributes and collect intercepted services
            if (TypeDiscoveryHelper.HasInterceptAttributes(typeSymbol))
            {
                var lifetime = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);
                if (lifetime.HasValue)
                {
                    var classLevelInterceptors = TypeDiscoveryHelper.GetInterceptAttributes(typeSymbol);
                    var methodLevelInterceptors = TypeDiscoveryHelper.GetMethodLevelInterceptAttributes(typeSymbol);
                    var methods = TypeDiscoveryHelper.GetInterceptedMethods(typeSymbol, classLevelInterceptors, methodLevelInterceptors);

                    if (methods.Count > 0)
                    {
                        var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                        var interfaces = TypeDiscoveryHelper.GetRegisterableInterfaces(typeSymbol);
                        var interfaceNames = interfaces.Select(i => TypeDiscoveryHelper.GetFullyQualifiedName(i)).ToArray();
                        
                        // Collect all unique interceptor types
                        var allInterceptorTypes = classLevelInterceptors
                            .Concat(methodLevelInterceptors)
                            .Select(i => i.InterceptorTypeName)
                            .Distinct()
                            .ToArray();
                        
                        var interceptedSourceFilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;

                        interceptedServices.Add(new DiscoveredInterceptedService(
                            typeName,
                            interfaceNames,
                            assembly.Name,
                            lifetime.Value,
                            methods.ToArray(),
                            allInterceptorTypes,
                            interceptedSourceFilePath));
                    }
                }
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
                    
                    // Check for [DeferToContainer] attribute - use declared types instead of discovered constructors
                    var deferredParams = TypeDiscoveryHelper.GetDeferToContainerParameterTypes(typeSymbol);
                    TypeDiscoveryHelper.ConstructorParameterInfo[] constructorParams;
                    if (deferredParams != null)
                    {
                        // DeferToContainer doesn't support keyed services - convert to simple params
                        constructorParams = deferredParams.Select(t => new TypeDiscoveryHelper.ConstructorParameterInfo(t)).ToArray();
                    }
                    else
                    {
                        constructorParams = TypeDiscoveryHelper.GetBestConstructorParametersWithKeys(typeSymbol)?.ToArray() ?? [];
                    }
                    
                    // Get source file path for breadcrumbs (null for external assemblies)
                    var sourceFilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;

                    injectableTypes.Add(new DiscoveredType(typeName, interfaceNames, assembly.Name, lifetime.Value, constructorParams, sourceFilePath));
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
                    var sourceFilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;
                    var order = TypeDiscoveryHelper.GetPluginOrder(typeSymbol);

                    pluginTypes.Add(new DiscoveredPlugin(typeName, interfaceNames, assembly.Name, attributeNames, sourceFilePath, order));
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

    private static string GenerateTypeRegistrySource(DiscoveryResult discoveryResult, string assemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Type Registry");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        builder.AppendLine();
        builder.AppendLine("using NexusLabs.Needlr;");
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

        GenerateInjectableTypesArray(builder, discoveryResult.InjectableTypes, breadcrumbs, projectDirectory);
        builder.AppendLine();
        GeneratePluginTypesArray(builder, discoveryResult.PluginTypes, breadcrumbs, projectDirectory);

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

        builder.AppendLine();
        GenerateApplyDecoratorsMethod(builder, discoveryResult.Decorators, discoveryResult.InterceptedServices.Count > 0, safeAssemblyName, breadcrumbs, projectDirectory);

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string GenerateModuleInitializerBootstrapSource(string assemblyName, IReadOnlyList<string> referencedAssemblies, BreadcrumbWriter breadcrumbs)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Source-Gen Bootstrap");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.Runtime.CompilerServices;");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();
        builder.AppendLine("internal static class NeedlrSourceGenModuleInitializer");
        builder.AppendLine("{");
        builder.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        builder.AppendLine("    internal static void Initialize()");
        builder.AppendLine("    {");
        
        // Generate ForceLoadAssemblies call if there are referenced assemblies with [GenerateTypeRegistry]
        if (referencedAssemblies.Count > 0)
        {
            builder.AppendLine("        // Force-load referenced assemblies to ensure their module initializers run");
            builder.AppendLine("        ForceLoadReferencedAssemblies();");
            builder.AppendLine();
        }
        
        builder.AppendLine("        global::NexusLabs.Needlr.Generators.NeedlrSourceGenBootstrap.Register(");
        builder.AppendLine($"            global::{safeAssemblyName}.Generated.TypeRegistry.GetInjectableTypes,");
        builder.AppendLine($"            global::{safeAssemblyName}.Generated.TypeRegistry.GetPluginTypes,");
        builder.AppendLine($"            services => global::{safeAssemblyName}.Generated.TypeRegistry.ApplyDecorators((IServiceCollection)services));");
        builder.AppendLine("    }");

        // Generate ForceLoadReferencedAssemblies method if needed
        if (referencedAssemblies.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("    /// <summary>");
            builder.AppendLine("    /// Forces referenced assemblies with [GenerateTypeRegistry] to load,");
            builder.AppendLine("    /// ensuring their module initializers execute and register their types.");
            builder.AppendLine("    /// </summary>");
            builder.AppendLine("    /// <remarks>");
            builder.AppendLine("    /// Without this, transitive dependencies that are never directly referenced");
            builder.AppendLine("    /// in code would not be loaded by the CLR, and their plugins would not be discovered.");
            builder.AppendLine("    /// </remarks>");
            builder.AppendLine("    [MethodImpl(MethodImplOptions.NoInlining)]");
            builder.AppendLine("    private static void ForceLoadReferencedAssemblies()");
            builder.AppendLine("    {");
            
            foreach (var referencedAssembly in referencedAssemblies)
            {
                var safeRefAssemblyName = SanitizeIdentifier(referencedAssembly);
                builder.AppendLine($"        _ = typeof(global::{safeRefAssemblyName}.Generated.TypeRegistry).Assembly;");
            }
            
            builder.AppendLine("    }");
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void GenerateInjectableTypesArray(StringBuilder builder, IReadOnlyList<DiscoveredType> types, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        builder.AppendLine("    private static readonly InjectableTypeInfo[] _types =");
        builder.AppendLine("    [");

        var typesByAssembly = types.GroupBy(t => t.AssemblyName).OrderBy(g => g.Key);

        foreach (var group in typesByAssembly)
        {
            breadcrumbs.WriteInlineComment(builder, "        ", $"From {group.Key}");

            foreach (var type in group.OrderBy(t => t.TypeName))
            {
                // Write breadcrumb for this type
                if (breadcrumbs.Level == BreadcrumbLevel.Verbose)
                {
                    var sourcePath = type.SourceFilePath != null 
                        ? BreadcrumbWriter.GetRelativeSourcePath(type.SourceFilePath, projectDirectory)
                        : $"[{type.AssemblyName}]";
                    var interfaces = type.InterfaceNames.Length > 0 
                        ? string.Join(", ", type.InterfaceNames.Select(i => i.Split('.').Last()))
                        : "none";
                    
                    breadcrumbs.WriteVerboseBox(builder, "        ",
                        $"{type.TypeName.Split('.').Last()} → {interfaces}",
                        $"Source: {sourcePath}",
                        $"Lifetime: {type.Lifetime}");
                }

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
                if (type.ConstructorParameters.Length > 0)
                {
                    var parameterExpressions = type.ConstructorParameters
                        .Select(p => p.IsKeyed 
                            ? $"sp.GetRequiredKeyedService<{p.TypeName}>(\"{EscapeStringLiteral(p.ServiceKey!)}\")"
                            : $"sp.GetRequiredService<{p.TypeName}>()");
                    builder.Append(string.Join(", ", parameterExpressions));
                }
                builder.AppendLine(")),");
            }
        }

        builder.AppendLine("    ];");
    }

    private static void GeneratePluginTypesArray(StringBuilder builder, IReadOnlyList<DiscoveredPlugin> plugins, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        builder.AppendLine("    private static readonly PluginTypeInfo[] _plugins =");
        builder.AppendLine("    [");

        // Sort plugins by Order first, then by TypeName for determinism
        var sortedPlugins = plugins
            .OrderBy(p => p.Order)
            .ThenBy(p => p.TypeName, StringComparer.Ordinal)
            .ToList();

        // Group for breadcrumb display, but maintain the sorted order
        var pluginsByAssembly = sortedPlugins.GroupBy(p => p.AssemblyName).OrderBy(g => g.Key);

        foreach (var group in pluginsByAssembly)
        {
            breadcrumbs.WriteInlineComment(builder, "        ", $"From {group.Key}");

            // Maintain order within assembly group
            foreach (var plugin in group.OrderBy(p => p.Order).ThenBy(p => p.TypeName, StringComparer.Ordinal))
            {
                // Write verbose breadcrumb for this plugin
                if (breadcrumbs.Level == BreadcrumbLevel.Verbose)
                {
                    var sourcePath = plugin.SourceFilePath != null 
                        ? BreadcrumbWriter.GetRelativeSourcePath(plugin.SourceFilePath, projectDirectory)
                        : $"[{plugin.AssemblyName}]";
                    var interfaces = plugin.InterfaceNames.Length > 0 
                        ? string.Join(", ", plugin.InterfaceNames.Select(i => i.Split('.').Last()))
                        : "none";
                    var orderInfo = plugin.Order != 0 ? $"Order: {plugin.Order}" : "Order: 0 (default)";
                    
                    breadcrumbs.WriteVerboseBox(builder, "        ",
                        $"Plugin: {plugin.TypeName.Split('.').Last()}",
                        $"Source: {sourcePath}",
                        $"Implements: {interfaces}",
                        orderInfo);
                }
                else if (breadcrumbs.Level == BreadcrumbLevel.Minimal && plugin.Order != 0)
                {
                    // Show order in minimal mode only if non-default
                    breadcrumbs.WriteInlineComment(builder, "        ", $"{plugin.TypeName.Split('.').Last()} (Order: {plugin.Order})");
                }

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
                    builder.Append("Array.Empty<Type>(), ");
                }
                else
                {
                    builder.Append("[");
                    builder.Append(string.Join(", ", plugin.AttributeNames.Select(a => $"typeof({a})")));
                    builder.Append("], ");
                }

                // Order
                builder.AppendLine($"{plugin.Order}),");
            }
        }

        builder.AppendLine("    ];");
    }

    private static void GenerateApplyDecoratorsMethod(StringBuilder builder, IReadOnlyList<DiscoveredDecorator> decorators, bool hasInterceptors, string safeAssemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Applies all discovered decorators and interceptors to the service collection.");
        builder.AppendLine("    /// Decorators are applied in order, with lower Order values applied first (closer to the original service).");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <param name=\"services\">The service collection to apply decorators to.</param>");
        builder.AppendLine("    public static void ApplyDecorators(IServiceCollection services)");
        builder.AppendLine("    {");

        if (decorators.Count == 0 && !hasInterceptors)
        {
            breadcrumbs.WriteInlineComment(builder, "        ", "No decorators or interceptors discovered");
        }
        else
        {
            if (decorators.Count > 0)
            {
                // Group decorators by service type and order by Order property
                var decoratorsByService = decorators
                    .GroupBy(d => d.ServiceTypeName)
                    .OrderBy(g => g.Key);

                foreach (var serviceGroup in decoratorsByService)
                {
                    // Write verbose breadcrumb for decorator chain
                    if (breadcrumbs.Level == BreadcrumbLevel.Verbose)
                    {
                        var chainItems = serviceGroup.OrderBy(d => d.Order).ToList();
                        var lines = new List<string>
                        {
                            "Resolution order (outer → inner → target):"
                        };
                        for (int i = 0; i < chainItems.Count; i++)
                        {
                            var dec = chainItems[i];
                            var sourcePath = dec.SourceFilePath != null 
                                ? BreadcrumbWriter.GetRelativeSourcePath(dec.SourceFilePath, projectDirectory)
                                : $"[{dec.AssemblyName}]";
                            lines.Add($"  {i + 1}. {dec.DecoratorTypeName.Split('.').Last()} (Order={dec.Order}) ← {sourcePath}");
                        }
                        lines.Add($"Triggered by: [DecoratorFor<{serviceGroup.Key.Split('.').Last()}>] attributes");
                        
                        breadcrumbs.WriteVerboseBox(builder, "        ",
                            $"Decorator Chain: {serviceGroup.Key.Split('.').Last()}",
                            lines.ToArray());
                    }
                    else
                    {
                        breadcrumbs.WriteInlineComment(builder, "        ", $"Decorators for {serviceGroup.Key}");
                    }

                    foreach (var decorator in serviceGroup.OrderBy(d => d.Order))
                    {
                        builder.AppendLine($"        services.AddDecorator<{decorator.ServiceTypeName}, {decorator.DecoratorTypeName}>(); // Order: {decorator.Order}");
                    }
                }
            }

            if (hasInterceptors)
            {
                builder.AppendLine();
                breadcrumbs.WriteInlineComment(builder, "        ", "Register intercepted services with their proxies");
                builder.AppendLine($"        global::{safeAssemblyName}.Generated.InterceptorRegistrations.RegisterInterceptedServices(services);");
            }
        }

        builder.AppendLine("    }");
    }

    private static string GenerateSignalRHubRegistrationsSource(IReadOnlyList<DiscoveredHubRegistration> hubRegistrations, string assemblyName, BreadcrumbWriter breadcrumbs)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr SignalR Hub Registrations");
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

    private static string GenerateSemanticKernelPluginsSource(IReadOnlyList<DiscoveredKernelPlugin> kernelPlugins, string assemblyName, BreadcrumbWriter breadcrumbs)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr SemanticKernel Plugins");
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

    private static string GenerateInterceptorProxiesSource(IReadOnlyList<DiscoveredInterceptedService> interceptedServices, string assemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Interceptor Proxies");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Reflection;");
        builder.AppendLine("using System.Threading.Tasks;");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        builder.AppendLine();
        builder.AppendLine("using NexusLabs.Needlr;");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();

        // Generate each proxy class
        foreach (var service in interceptedServices)
        {
            GenerateInterceptorProxyClass(builder, service, breadcrumbs, projectDirectory);
            builder.AppendLine();
        }

        // Generate the registration helper
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Helper class for registering intercepted services.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine("public static class InterceptorRegistrations");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Registers all intercepted services and their proxies.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <param name=\"services\">The service collection to register to.</param>");
        builder.AppendLine("    public static void RegisterInterceptedServices(IServiceCollection services)");
        builder.AppendLine("    {");

        foreach (var service in interceptedServices)
        {
            var proxyTypeName = GetProxyTypeName(service.TypeName);
            var lifetime = service.Lifetime switch
            {
                GeneratorLifetime.Singleton => "Singleton",
                GeneratorLifetime.Scoped => "Scoped",
                GeneratorLifetime.Transient => "Transient",
                _ => "Scoped"
            };

            // Register all interceptor types
            foreach (var interceptorType in service.AllInterceptorTypeNames)
            {
                breadcrumbs.WriteInlineComment(builder, "        ", $"Register interceptor: {interceptorType.Split('.').Last()}");
                builder.AppendLine($"        if (!services.Any(d => d.ServiceType == typeof({interceptorType})))");
                builder.AppendLine($"            services.Add{lifetime}<{interceptorType}>();");
            }

            // Register the actual implementation type
            builder.AppendLine($"        // Register actual implementation");
            builder.AppendLine($"        services.Add{lifetime}<{service.TypeName}>();");

            // Register proxy for each interface
            foreach (var iface in service.InterfaceNames)
            {
                builder.AppendLine($"        // Register proxy for {iface}");
                builder.AppendLine($"        services.Add{lifetime}<{iface}>(sp => new {proxyTypeName}(");
                builder.AppendLine($"            sp.GetRequiredService<{service.TypeName}>(),");
                builder.AppendLine($"            sp));");
            }
        }

        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Gets the number of intercepted services discovered at compile time.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine($"    public static int Count => {interceptedServices.Count};");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string GenerateDiagnosticsSource(DiscoveryResult discoveryResult, string assemblyName, string? projectDirectory, DiagnosticOptions options)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("// Needlr Diagnostic Output Metadata");
        builder.AppendLine($"// Generated: {timestamp} UTC");
        builder.AppendLine("// This file contains diagnostic markdown content for extraction by MSBuild.");
        builder.AppendLine();
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Contains diagnostic output markdown content generated at compile time.");
        builder.AppendLine("/// Use NeedlrDiagnostics MSBuild property to enable file generation.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine("internal static class NeedlrDiagnostics");
        builder.AppendLine("{");

        // Output path for MSBuild target
        builder.AppendLine($"    public const string OutputPath = @\"{EscapeStringLiteral(options.OutputPath)}\";");
        builder.AppendLine();

        // Generate all three diagnostic outputs when enabled
        var dependencyGraphContent = GenerateDependencyGraphMarkdown(discoveryResult, assemblyName, timestamp, options.TypeFilter);
        builder.AppendLine("    /// <summary>DependencyGraph.md content</summary>");
        builder.AppendLine($"    public const string DependencyGraph = @\"{EscapeVerbatimStringLiteral(dependencyGraphContent)}\";");
        builder.AppendLine();

        var lifetimeSummaryContent = GenerateLifetimeSummaryMarkdown(discoveryResult, assemblyName, timestamp, options.TypeFilter);
        builder.AppendLine("    /// <summary>LifetimeSummary.md content</summary>");
        builder.AppendLine($"    public const string LifetimeSummary = @\"{EscapeVerbatimStringLiteral(lifetimeSummaryContent)}\";");
        builder.AppendLine();

        var registrationIndexContent = GenerateRegistrationIndexMarkdown(discoveryResult, assemblyName, projectDirectory, timestamp, options.TypeFilter);
        builder.AppendLine("    /// <summary>RegistrationIndex.md content</summary>");
        builder.AppendLine($"    public const string RegistrationIndex = @\"{EscapeVerbatimStringLiteral(registrationIndexContent)}\";");
        builder.AppendLine();

        var analyzerStatusContent = GenerateAnalyzerStatusMarkdown(timestamp);
        builder.AppendLine("    /// <summary>AnalyzerStatus.md content</summary>");
        builder.AppendLine($"    public const string AnalyzerStatus = @\"{EscapeVerbatimStringLiteral(analyzerStatusContent)}\";");

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string GenerateDependencyGraphMarkdown(DiscoveryResult discovery, string assemblyName, string timestamp, HashSet<string> typeFilter)
    {
        var sb = new StringBuilder();
        var types = FilterTypes(discovery.InjectableTypes, typeFilter);

        sb.AppendLine("# Needlr Dependency Graph");
        sb.AppendLine();
        sb.AppendLine($"Generated: {timestamp} UTC");
        sb.AppendLine($"Assembly: {assemblyName}");
        sb.AppendLine();

        var singletons = types.Where(t => t.Lifetime == GeneratorLifetime.Singleton).ToList();
        var scopeds = types.Where(t => t.Lifetime == GeneratorLifetime.Scoped).ToList();
        var transients = types.Where(t => t.Lifetime == GeneratorLifetime.Transient).ToList();

        sb.AppendLine("## Service Dependencies");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph TD");

        // Emit subgraphs by lifetime
        if (singletons.Any())
        {
            sb.AppendLine("    subgraph Singleton");
            foreach (var type in singletons)
                sb.AppendLine($"        {GetMermaidNodeId(type.TypeName)}[\"{GetShortTypeName(type.TypeName)}\"]");
            sb.AppendLine("    end");
        }
        if (scopeds.Any())
        {
            sb.AppendLine("    subgraph Scoped");
            foreach (var type in scopeds)
                sb.AppendLine($"        {GetMermaidNodeId(type.TypeName)}[\"{GetShortTypeName(type.TypeName)}\"]");
            sb.AppendLine("    end");
        }
        if (transients.Any())
        {
            sb.AppendLine("    subgraph Transient");
            foreach (var type in transients)
                sb.AppendLine($"        {GetMermaidNodeId(type.TypeName)}[\"{GetShortTypeName(type.TypeName)}\"]");
            sb.AppendLine("    end");
        }

        // Emit edges for dependencies
        var typeNames = new HashSet<string>(types.Select(t => GetShortTypeName(t.TypeName)), StringComparer.Ordinal);
        foreach (var type in types)
        {
            foreach (var dep in type.ConstructorParameterTypes)
            {
                var depShort = GetShortTypeName(dep);
                // Find if we have a type implementing this interface
                var matchingType = types.FirstOrDefault(t =>
                    GetShortTypeName(t.TypeName) == depShort ||
                    t.InterfaceNames.Any(i => GetShortTypeName(i) == depShort));

                if (matchingType.TypeName != null)
                    sb.AppendLine($"    {GetMermaidNodeId(type.TypeName)} --> {GetMermaidNodeId(matchingType.TypeName)}");
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();

        // Dependency details table
        sb.AppendLine("## Dependency Details");
        sb.AppendLine();
        sb.AppendLine("| Service | Lifetime | Dependencies |");
        sb.AppendLine("|---------|----------|--------------|");

        foreach (var type in types.OrderBy(t => t.TypeName))
        {
            var deps = type.ConstructorParameterTypes.Any()
                ? string.Join(", ", type.ConstructorParameterTypes.Select(GetShortTypeName))
                : "-";
            sb.AppendLine($"| {GetShortTypeName(type.TypeName)} | {type.Lifetime} | {deps} |");
        }

        return sb.ToString();
    }

    private static string GenerateLifetimeSummaryMarkdown(DiscoveryResult discovery, string assemblyName, string timestamp, HashSet<string> typeFilter)
    {
        var sb = new StringBuilder();
        var types = FilterTypes(discovery.InjectableTypes, typeFilter);

        var singletons = types.Where(t => t.Lifetime == GeneratorLifetime.Singleton).ToList();
        var scopeds = types.Where(t => t.Lifetime == GeneratorLifetime.Scoped).ToList();
        var transients = types.Where(t => t.Lifetime == GeneratorLifetime.Transient).ToList();
        var total = types.Count;

        sb.AppendLine("# Needlr Lifetime Summary");
        sb.AppendLine();
        sb.AppendLine($"Generated: {timestamp} UTC");
        sb.AppendLine($"Assembly: {assemblyName}");
        sb.AppendLine();

        sb.AppendLine("## Registration Counts");
        sb.AppendLine();
        sb.AppendLine("| Lifetime | Count | % |");
        sb.AppendLine("|----------|-------|---|");

        if (total > 0)
        {
            sb.AppendLine($"| Singleton | {singletons.Count} | {Percentage(singletons.Count, total)}% |");
            sb.AppendLine($"| Scoped | {scopeds.Count} | {Percentage(scopeds.Count, total)}% |");
            sb.AppendLine($"| Transient | {transients.Count} | {Percentage(transients.Count, total)}% |");
            sb.AppendLine($"| **Total** | **{total}** | 100% |");
        }
        else
        {
            sb.AppendLine("| (none) | 0 | - |");
        }

        sb.AppendLine();

        // List by category
        if (singletons.Any())
        {
            sb.AppendLine($"## Singleton ({singletons.Count})");
            sb.AppendLine();
            foreach (var type in singletons.OrderBy(t => t.TypeName))
                sb.AppendLine($"- {GetShortTypeName(type.TypeName)}");
            sb.AppendLine();
        }

        if (scopeds.Any())
        {
            sb.AppendLine($"## Scoped ({scopeds.Count})");
            sb.AppendLine();
            foreach (var type in scopeds.OrderBy(t => t.TypeName))
                sb.AppendLine($"- {GetShortTypeName(type.TypeName)}");
            sb.AppendLine();
        }

        if (transients.Any())
        {
            sb.AppendLine($"## Transient ({transients.Count})");
            sb.AppendLine();
            foreach (var type in transients.OrderBy(t => t.TypeName))
                sb.AppendLine($"- {GetShortTypeName(type.TypeName)}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GenerateRegistrationIndexMarkdown(DiscoveryResult discovery, string assemblyName, string? projectDirectory, string timestamp, HashSet<string> typeFilter)
    {
        var sb = new StringBuilder();
        var types = FilterTypes(discovery.InjectableTypes, typeFilter);
        var plugins = FilterPluginTypes(discovery.PluginTypes, typeFilter);
        var decorators = FilterDecorators(discovery.Decorators, typeFilter);

        sb.AppendLine("# Needlr Registration Index");
        sb.AppendLine();
        sb.AppendLine($"Generated: {timestamp} UTC");
        sb.AppendLine($"Assembly: {assemblyName}");
        sb.AppendLine();

        // Services table
        sb.AppendLine($"## Services ({types.Count})");
        sb.AppendLine();

        if (types.Any())
        {
            sb.AppendLine("| # | Interface | Implementation | Lifetime | Source |");
            sb.AppendLine("|---|-----------|----------------|----------|--------|");

            var index = 1;
            foreach (var type in types.OrderBy(t => t.TypeName))
            {
                var iface = type.InterfaceNames.FirstOrDefault() ?? "-";
                var source = BreadcrumbWriter.GetRelativeSourcePath(type.SourceFilePath, projectDirectory);
                sb.AppendLine($"| {index} | {GetShortTypeName(iface)} | {GetShortTypeName(type.TypeName)} | {type.Lifetime} | {source} |");
                index++;
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("No injectable services discovered.");
            sb.AppendLine();
        }

        // Decorators section
        if (decorators.Any())
        {
            var decoratorsByTarget = decorators
                .GroupBy(d => d.ServiceTypeName)
                .OrderBy(g => g.Key);

            sb.AppendLine($"## Decorators ({decorators.Count})");
            sb.AppendLine();
            sb.AppendLine("| Service | Decorator Chain |");
            sb.AppendLine("|---------|-----------------|");

            foreach (var group in decoratorsByTarget)
            {
                var chain = string.Join(" → ",
                    group.OrderBy(d => d.Order)
                         .Select(d => GetShortTypeName(d.DecoratorTypeName)));
                sb.AppendLine($"| {GetShortTypeName(group.Key)} | {chain} |");
            }
            sb.AppendLine();
        }

        // Plugins section
        if (plugins.Any())
        {
            var orderedPlugins = plugins.OrderBy(p => p.Order).ThenBy(p => p.TypeName);

            sb.AppendLine($"## Plugins ({plugins.Count})");
            sb.AppendLine();
            sb.AppendLine("| Order | Plugin | Interfaces |");
            sb.AppendLine("|-------|--------|------------|");

            foreach (var plugin in orderedPlugins)
            {
                var interfaces = string.Join(", ", plugin.InterfaceNames.Select(GetShortTypeName));
                sb.AppendLine($"| {plugin.Order} | {GetShortTypeName(plugin.TypeName)} | {interfaces} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GenerateAnalyzerStatusMarkdown(string timestamp)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Needlr Analyzer Status");
        sb.AppendLine();
        sb.AppendLine($"Generated: {timestamp} UTC");
        sb.AppendLine();
        sb.AppendLine("## Active Analyzers");
        sb.AppendLine();
        sb.AppendLine("| ID | Name | Status | Default Severity | Description |");
        sb.AppendLine("|:---|:-----|:-------|:-----------------|:------------|");
        sb.AppendLine("| NDLRCOR001 | Reflection in AOT | ⚪ Conditional | Error | Detects reflection APIs in AOT projects |");
        sb.AppendLine("| NDLRCOR002 | Plugin Constructor | ✅ Active | Warning | Plugins should have parameterless constructors |");
        sb.AppendLine("| NDLRCOR003 | DeferToContainer in Generated | ✅ Active | Error | [DeferToContainer] must be on user code |");
        sb.AppendLine("| NDLRCOR004 | Global Namespace Type | ✅ Active | Warning | Types in global namespace may not be discovered |");
        sb.AppendLine("| NDLRCOR005 | Lifetime Mismatch | ✅ Active | Warning | Detects captive dependencies |");
        sb.AppendLine("| NDLRCOR006 | Circular Dependency | ✅ Active | Error | Detects circular service dependencies |");
        sb.AppendLine("| NDLRCOR007 | Intercept Type | ✅ Active | Error | Intercept type must implement IMethodInterceptor |");
        sb.AppendLine("| NDLRCOR008 | Intercept Without Interface | ✅ Active | Warning | [Intercept] requires interface-based class |");
        sb.AppendLine("| NDLRCOR009 | Lazy Resolution | ✅ Active | Info | Lazy<T> references undiscovered type |");
        sb.AppendLine("| NDLRCOR010 | Collection Resolution | ✅ Active | Info | IEnumerable<T> has no implementations |");
        sb.AppendLine("| NDLRCOR011 | Keyed Service Usage | ✅ Active | Info | Tracks [FromKeyedServices] parameter usage |");
        sb.AppendLine();
        sb.AppendLine("## Mode");
        sb.AppendLine();
        sb.AppendLine("**Source Generation**: Enabled (GenerateTypeRegistry detected)");
        sb.AppendLine();
        sb.AppendLine("## Configuration");
        sb.AppendLine();
        sb.AppendLine("Analyzer severity can be configured via `.editorconfig`:");
        sb.AppendLine();
        sb.AppendLine("```ini");
        sb.AppendLine("# Example: Suppress Lazy resolution warnings");
        sb.AppendLine("dotnet_diagnostic.NDLRCOR009.severity = none");
        sb.AppendLine();
        sb.AppendLine("# Example: Promote to warning");
        sb.AppendLine("dotnet_diagnostic.NDLRCOR009.severity = warning");
        sb.AppendLine("```");
        sb.AppendLine();

        return sb.ToString();
    }

    private static IReadOnlyList<DiscoveredType> FilterTypes(IReadOnlyList<DiscoveredType> types, HashSet<string> filter)
    {
        if (filter == null || filter.Count == 0)
            return types;

        return types.Where(t => 
            filter.Contains(t.TypeName) ||                                      // global::TestApp.OrderService
            filter.Contains(GetShortTypeName(t.TypeName)) ||                    // OrderService
            filter.Contains(StripGlobalPrefix(t.TypeName)))                     // TestApp.OrderService
                    .ToList();
    }

    private static IReadOnlyList<DiscoveredPlugin> FilterPluginTypes(IReadOnlyList<DiscoveredPlugin> plugins, HashSet<string> filter)
    {
        if (filter == null || filter.Count == 0)
            return plugins;

        return plugins.Where(p => 
            filter.Contains(p.TypeName) ||
            filter.Contains(GetShortTypeName(p.TypeName)) ||
            filter.Contains(StripGlobalPrefix(p.TypeName)))
                      .ToList();
    }

    private static IReadOnlyList<DiscoveredDecorator> FilterDecorators(IReadOnlyList<DiscoveredDecorator> decorators, HashSet<string> filter)
    {
        if (filter == null || filter.Count == 0)
            return decorators;

        return decorators.Where(d => 
            filter.Contains(d.DecoratorTypeName) ||
            filter.Contains(GetShortTypeName(d.DecoratorTypeName)) ||
            filter.Contains(StripGlobalPrefix(d.DecoratorTypeName)) ||
            filter.Contains(d.ServiceTypeName) ||
            filter.Contains(GetShortTypeName(d.ServiceTypeName)) ||
            filter.Contains(StripGlobalPrefix(d.ServiceTypeName)))
                         .ToList();
    }

    private static string StripGlobalPrefix(string name)
    {
        return name.StartsWith("global::", StringComparison.Ordinal) 
            ? name.Substring(8) 
            : name;
    }

    private static string GetMermaidNodeId(string typeName)
    {
        return GetShortTypeName(typeName).Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "_");
    }

    private static int Percentage(int count, int total)
    {
        if (total == 0) return 0;
        return (int)Math.Round(100.0 * count / total);
    }

    private static string EscapeStringLiteral(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string EscapeVerbatimStringLiteral(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        // In verbatim strings, only double-quotes need escaping (by doubling them)
        return value.Replace("\"", "\"\"");
    }

    private static void GenerateInterceptorProxyClass(StringBuilder builder, DiscoveredInterceptedService service, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var proxyTypeName = GetProxyTypeName(service.TypeName);
        var shortTypeName = GetShortTypeName(service.TypeName);

        // Write verbose breadcrumb for interceptor proxy
        if (breadcrumbs.Level == BreadcrumbLevel.Verbose)
        {
            var sourcePath = service.SourceFilePath != null 
                ? BreadcrumbWriter.GetRelativeSourcePath(service.SourceFilePath, projectDirectory)
                : $"[{service.AssemblyName}]";
            
            var interceptorsList = service.AllInterceptorTypeNames
                .Select((t, i) => $"  {i + 1}. {t.Split('.').Last()}")
                .ToArray();
            
            var proxiedMethods = service.Methods
                .Where(m => m.InterceptorTypeNames.Length > 0)
                .Select(m => m.Name)
                .ToArray();
            var forwardedMethods = service.Methods
                .Where(m => m.InterceptorTypeNames.Length == 0)
                .Select(m => m.Name)
                .ToArray();

            var lines = new List<string>
            {
                $"Source: {sourcePath}",
                $"Target Interface: {string.Join(", ", service.InterfaceNames.Select(i => i.Split('.').Last()))}",
                "Interceptors (execution order):"
            };
            lines.AddRange(interceptorsList);
            lines.Add($"Methods proxied: {(proxiedMethods.Length > 0 ? string.Join(", ", proxiedMethods) : "none")}");
            lines.Add($"Methods forwarded: {(forwardedMethods.Length > 0 ? string.Join(", ", forwardedMethods) : "none")}");
            
            breadcrumbs.WriteVerboseBox(builder, "",
                $"Interceptor Proxy: {shortTypeName}",
                lines.ToArray());
        }

        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Interceptor proxy for {service.TypeName}.");
        builder.AppendLine("/// Routes method calls through configured interceptors.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");

        // Implement all interfaces
        builder.Append($"internal sealed class {proxyTypeName}");
        if (service.InterfaceNames.Length > 0)
        {
            builder.Append(" : ");
            builder.Append(string.Join(", ", service.InterfaceNames));
        }
        builder.AppendLine();
        builder.AppendLine("{");

        // Fields
        builder.AppendLine($"    private readonly {service.TypeName} _target;");
        builder.AppendLine("    private readonly IServiceProvider _serviceProvider;");
        builder.AppendLine();

        // Static MethodInfo fields for each method
        var methodIndex = 0;
        foreach (var method in service.Methods)
        {
            builder.AppendLine($"    private static readonly MethodInfo _method{methodIndex} = typeof({method.InterfaceTypeName}).GetMethod(nameof({method.InterfaceTypeName}.{method.Name}))!;");
            methodIndex++;
        }
        builder.AppendLine();

        // Constructor
        builder.AppendLine($"    public {proxyTypeName}({service.TypeName} target, IServiceProvider serviceProvider)");
        builder.AppendLine("    {");
        builder.AppendLine("        _target = target ?? throw new ArgumentNullException(nameof(target));");
        builder.AppendLine("        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));");
        builder.AppendLine("    }");
        builder.AppendLine();

        // Generate each method
        methodIndex = 0;
        foreach (var method in service.Methods)
        {
            GenerateInterceptedMethod(builder, method, methodIndex, service.TypeName, breadcrumbs);
            methodIndex++;
        }

        builder.AppendLine("}");
    }

    private static void GenerateInterceptedMethod(StringBuilder builder, TypeDiscoveryHelper.InterceptedMethodInfo method, int methodIndex, string targetTypeName, BreadcrumbWriter breadcrumbs)
    {
        var parameterList = method.GetParameterList();
        var argumentList = method.GetArgumentList();

        // Write breadcrumb for method
        if (method.InterceptorTypeNames.Length > 0)
        {
            var interceptorNames = string.Join(" → ", method.InterceptorTypeNames.Select(t => t.Split('.').Last()));
            breadcrumbs.WriteInlineComment(builder, "    ", $"{method.Name}: {interceptorNames}");
        }
        else
        {
            breadcrumbs.WriteInlineComment(builder, "    ", $"{method.Name}: direct forward (no interceptors)");
        }

        builder.AppendLine($"    public {method.ReturnType} {method.Name}({parameterList})");
        builder.AppendLine("    {");

        // If no interceptors, just forward directly to target
        if (method.InterceptorTypeNames.Length == 0)
        {
            if (method.IsVoid)
            {
                builder.AppendLine($"        _target.{method.Name}({argumentList});");
            }
            else
            {
                builder.AppendLine($"        return _target.{method.Name}({argumentList});");
            }
            builder.AppendLine("    }");
            builder.AppendLine();
            return;
        }

        // Build interceptor chain
        var interceptorCount = method.InterceptorTypeNames.Length;
        builder.AppendLine($"        var interceptors = new IMethodInterceptor[{interceptorCount}];");
        for (var i = 0; i < interceptorCount; i++)
        {
            builder.AppendLine($"        interceptors[{i}] = _serviceProvider.GetRequiredService<{method.InterceptorTypeNames[i]}>();");
        }
        builder.AppendLine();

        // Create arguments array
        if (method.Parameters.Count > 0)
        {
            builder.AppendLine($"        var args = new object?[] {{ {string.Join(", ", method.Parameters.Select(p => p.Name))} }};");
        }
        else
        {
            builder.AppendLine("        var args = Array.Empty<object?>();");
        }
        builder.AppendLine();

        // Build the proceed chain - start from innermost (actual call) and wrap outward
        builder.AppendLine("        // Build the interceptor chain from inside out");
        builder.AppendLine("        Func<ValueTask<object?>> proceed = async () =>");
        builder.AppendLine("        {");

        if (method.IsVoid)
        {
            builder.AppendLine($"            _target.{method.Name}({argumentList});");
            builder.AppendLine("            return null;");
        }
        else if (method.IsAsync)
        {
            // Check if the return type is Task<T> or ValueTask<T> (has a result)
            var hasResult = !method.ReturnType.Equals("global::System.Threading.Tasks.Task", StringComparison.Ordinal) &&
                           !method.ReturnType.Equals("global::System.Threading.Tasks.ValueTask", StringComparison.Ordinal);
            
            if (hasResult)
            {
                builder.AppendLine($"            var result = await _target.{method.Name}({argumentList});");
                builder.AppendLine("            return result;");
            }
            else
            {
                builder.AppendLine($"            await _target.{method.Name}({argumentList});");
                builder.AppendLine("            return null;");
            }
        }
        else
        {
            builder.AppendLine($"            var result = _target.{method.Name}({argumentList});");
            builder.AppendLine("            return result;");
        }

        builder.AppendLine("        };");
        builder.AppendLine();

        // Wrap each interceptor, from last to first (so first interceptor is outermost)
        builder.AppendLine("        for (var i = interceptors.Length - 1; i >= 0; i--)");
        builder.AppendLine("        {");
        builder.AppendLine("            var interceptor = interceptors[i];");
        builder.AppendLine("            var nextProceed = proceed;");
        builder.AppendLine($"            proceed = () => interceptor.InterceptAsync(new MethodInvocation(_target, _method{methodIndex}, args, nextProceed));");
        builder.AppendLine("        }");
        builder.AppendLine();

        // Invoke the chain and return the result
        if (method.IsVoid)
        {
            builder.AppendLine("        proceed().AsTask().GetAwaiter().GetResult();");
        }
        else if (method.IsAsync)
        {
            var hasResult = !method.ReturnType.Equals("global::System.Threading.Tasks.Task", StringComparison.Ordinal) &&
                           !method.ReturnType.Equals("global::System.Threading.Tasks.ValueTask", StringComparison.Ordinal);
            
            if (hasResult)
            {
                // Extract the inner type from Task<T> or ValueTask<T>
                var innerType = ExtractGenericTypeArgument(method.ReturnType);
                if (method.ReturnType.StartsWith("global::System.Threading.Tasks.ValueTask<", StringComparison.Ordinal))
                {
                    builder.AppendLine($"        return new {method.ReturnType}(proceed().AsTask().ContinueWith(t => ({innerType})t.Result!));");
                }
                else
                {
                    // Task<T>
                    builder.AppendLine($"        return proceed().AsTask().ContinueWith(t => ({innerType})t.Result!);");
                }
            }
            else
            {
                // Task or ValueTask without result
                if (method.ReturnType.StartsWith("global::System.Threading.Tasks.ValueTask", StringComparison.Ordinal))
                {
                    builder.AppendLine("        return new global::System.Threading.Tasks.ValueTask(proceed().AsTask());");
                }
                else
                {
                    builder.AppendLine("        return proceed().AsTask();");
                }
            }
        }
        else
        {
            // Synchronous with return value
            builder.AppendLine($"        return ({method.ReturnType})proceed().AsTask().GetAwaiter().GetResult()!;");
        }

        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static string GetProxyTypeName(string fullyQualifiedTypeName)
    {
        var shortName = GetShortTypeName(fullyQualifiedTypeName);
        return $"{shortName}_InterceptorProxy";
    }

    private static string GetShortTypeName(string fullyQualifiedTypeName)
    {
        // Remove global:: prefix and get just the type name
        var name = fullyQualifiedTypeName;
        if (name.StartsWith("global::", StringComparison.Ordinal))
            name = name.Substring(8);
        
        var lastDot = name.LastIndexOf('.');
        return lastDot >= 0 ? name.Substring(lastDot + 1) : name;
    }

    private static string ExtractGenericTypeArgument(string genericTypeName)
    {
        // Extract T from Task<T> or ValueTask<T>
        var openBracket = genericTypeName.IndexOf('<');
        var closeBracket = genericTypeName.LastIndexOf('>');
        if (openBracket >= 0 && closeBracket > openBracket)
        {
            return genericTypeName.Substring(openBracket + 1, closeBracket - openBracket - 1);
        }
        return "object";
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

    /// <summary>
    /// Discovers all referenced assemblies that have the [GenerateTypeRegistry] attribute.
    /// These assemblies need to be force-loaded to ensure their module initializers run.
    /// </summary>
    private static IReadOnlyList<string> DiscoverReferencedAssembliesWithTypeRegistry(Compilation compilation)
    {
        var result = new List<string>();
        
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                // Skip the current assembly
                if (SymbolEqualityComparer.Default.Equals(assemblySymbol, compilation.Assembly))
                    continue;
                    
                if (TypeDiscoveryHelper.HasGenerateTypeRegistryAttribute(assemblySymbol))
                {
                    result.Add(assemblySymbol.Name);
                }
            }
        }
        
        return result;
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
        public DiscoveredType(string typeName, string[] interfaceNames, string assemblyName, GeneratorLifetime lifetime, TypeDiscoveryHelper.ConstructorParameterInfo[] constructorParameters, string? sourceFilePath = null)
        {
            TypeName = typeName;
            InterfaceNames = interfaceNames;
            AssemblyName = assemblyName;
            Lifetime = lifetime;
            ConstructorParameters = constructorParameters;
            SourceFilePath = sourceFilePath;
        }

        public string TypeName { get; }
        public string[] InterfaceNames { get; }
        public string AssemblyName { get; }
        public GeneratorLifetime Lifetime { get; }
        public TypeDiscoveryHelper.ConstructorParameterInfo[] ConstructorParameters { get; }
        public string? SourceFilePath { get; }

        /// <summary>
        /// Gets the constructor parameter types (for backward compatibility with existing code paths).
        /// </summary>
        public string[] ConstructorParameterTypes => ConstructorParameters.Select(p => p.TypeName).ToArray();

        /// <summary>
        /// True if any constructor parameters are keyed services.
        /// </summary>
        public bool HasKeyedParameters => ConstructorParameters.Any(p => p.IsKeyed);
    }

    private readonly struct DiscoveredPlugin
    {
        public DiscoveredPlugin(string typeName, string[] interfaceNames, string assemblyName, string[] attributeNames, string? sourceFilePath = null, int order = 0)
        {
            TypeName = typeName;
            InterfaceNames = interfaceNames;
            AssemblyName = assemblyName;
            AttributeNames = attributeNames;
            SourceFilePath = sourceFilePath;
            Order = order;
        }

        public string TypeName { get; }
        public string[] InterfaceNames { get; }
        public string AssemblyName { get; }
        public string[] AttributeNames { get; }
        public string? SourceFilePath { get; }
        public int Order { get; }
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

    private readonly struct DiscoveredDecorator
    {
        public DiscoveredDecorator(string decoratorTypeName, string serviceTypeName, int order, string assemblyName, string? sourceFilePath = null)
        {
            DecoratorTypeName = decoratorTypeName;
            ServiceTypeName = serviceTypeName;
            Order = order;
            AssemblyName = assemblyName;
            SourceFilePath = sourceFilePath;
        }

        public string DecoratorTypeName { get; }
        public string ServiceTypeName { get; }
        public int Order { get; }
        public string AssemblyName { get; }
        public string? SourceFilePath { get; }
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
            IReadOnlyList<DiscoveredDecorator> decorators,
            IReadOnlyList<InaccessibleType> inaccessibleTypes,
            IReadOnlyList<MissingTypeRegistryPlugin> missingTypeRegistryPlugins,
            IReadOnlyList<DiscoveredInterceptedService> interceptedServices)
        {
            InjectableTypes = injectableTypes;
            PluginTypes = pluginTypes;
            HubRegistrations = hubRegistrations;
            KernelPlugins = kernelPlugins;
            Decorators = decorators;
            InaccessibleTypes = inaccessibleTypes;
            MissingTypeRegistryPlugins = missingTypeRegistryPlugins;
            InterceptedServices = interceptedServices;
        }

        public IReadOnlyList<DiscoveredType> InjectableTypes { get; }
        public IReadOnlyList<DiscoveredPlugin> PluginTypes { get; }
        public IReadOnlyList<DiscoveredHubRegistration> HubRegistrations { get; }
        public IReadOnlyList<DiscoveredKernelPlugin> KernelPlugins { get; }
        public IReadOnlyList<DiscoveredDecorator> Decorators { get; }
        public IReadOnlyList<InaccessibleType> InaccessibleTypes { get; }
        public IReadOnlyList<MissingTypeRegistryPlugin> MissingTypeRegistryPlugins { get; }
        public IReadOnlyList<DiscoveredInterceptedService> InterceptedServices { get; }
    }

    private readonly struct DiscoveredInterceptedService
    {
        public DiscoveredInterceptedService(
            string typeName,
            string[] interfaceNames,
            string assemblyName,
            GeneratorLifetime lifetime,
            TypeDiscoveryHelper.InterceptedMethodInfo[] methods,
            string[] allInterceptorTypeNames,
            string? sourceFilePath = null)
        {
            TypeName = typeName;
            InterfaceNames = interfaceNames;
            AssemblyName = assemblyName;
            Lifetime = lifetime;
            Methods = methods;
            AllInterceptorTypeNames = allInterceptorTypeNames;
            SourceFilePath = sourceFilePath;
        }

        public string TypeName { get; }
        public string[] InterfaceNames { get; }
        public string AssemblyName { get; }
        public GeneratorLifetime Lifetime { get; }
        public TypeDiscoveryHelper.InterceptedMethodInfo[] Methods { get; }
        public string[] AllInterceptorTypeNames { get; }
        public string? SourceFilePath { get; }
    }
}
