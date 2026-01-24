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
        // ForAttributeWithMetadataName doesn't work for assembly-level attributes.
        // Instead, we register directly on the compilation provider and check
        // compilation.Assembly.GetAttributes() for [GenerateTypeRegistry].
        context.RegisterSourceOutput(context.CompilationProvider, static (spc, compilation) =>
        {
            var attributeInfo = GetAttributeInfoFromCompilation(compilation);
            if (attributeInfo == null)
                return;

            var info = attributeInfo.Value;
            var assemblyName = compilation.AssemblyName ?? "Generated";

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

            var sourceText = GenerateTypeRegistrySource(discoveryResult, assemblyName);
            spc.AddSource("TypeRegistry.g.cs", SourceText.From(sourceText, Encoding.UTF8));

            // Discover referenced assemblies with [GenerateTypeRegistry] for forced loading
            // Note: Order of force-loading doesn't matter; ordering is applied at service registration time
            var referencedAssemblies = DiscoverReferencedAssembliesWithTypeRegistry(compilation)
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var bootstrapText = GenerateModuleInitializerBootstrapSource(assemblyName, referencedAssemblies);
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

            // Generate interceptor proxy classes if any were discovered
            if (discoveryResult.InterceptedServices.Count > 0)
            {
                var interceptorProxiesText = GenerateInterceptorProxiesSource(discoveryResult.InterceptedServices, assemblyName);
                spc.AddSource("InterceptorProxies.g.cs", SourceText.From(interceptorProxiesText, Encoding.UTF8));
            }
        });
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
                decorators.Add(new DiscoveredDecorator(
                    decoratorInfo.DecoratorTypeName,
                    decoratorInfo.ServiceTypeName,
                    decoratorInfo.Order,
                    assembly.Name));
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

                        interceptedServices.Add(new DiscoveredInterceptedService(
                            typeName,
                            interfaceNames,
                            assembly.Name,
                            lifetime.Value,
                            methods.ToArray(),
                            allInterceptorTypes));
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
                    var constructorParams = deferredParams ?? TypeDiscoveryHelper.GetBestConstructorParameters(typeSymbol) ?? [];

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

        builder.AppendLine();
        GenerateApplyDecoratorsMethod(builder, discoveryResult.Decorators, discoveryResult.InterceptedServices.Count > 0, safeAssemblyName);

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string GenerateModuleInitializerBootstrapSource(string assemblyName, IReadOnlyList<string> referencedAssemblies)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        builder.AppendLine("// <auto-generated/>");
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

    private static void GenerateApplyDecoratorsMethod(StringBuilder builder, IReadOnlyList<DiscoveredDecorator> decorators, bool hasInterceptors, string safeAssemblyName)
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
            builder.AppendLine("        // No decorators or interceptors discovered");
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
                    builder.AppendLine($"        // Decorators for {serviceGroup.Key}");
                    foreach (var decorator in serviceGroup.OrderBy(d => d.Order))
                    {
                        builder.AppendLine($"        services.AddDecorator<{decorator.ServiceTypeName}, {decorator.DecoratorTypeName}>(); // Order: {decorator.Order}, from {decorator.AssemblyName}");
                    }
                }
            }

            if (hasInterceptors)
            {
                builder.AppendLine();
                builder.AppendLine("        // Register intercepted services with their proxies");
                builder.AppendLine($"        global::{safeAssemblyName}.Generated.InterceptorRegistrations.RegisterInterceptedServices(services);");
            }
        }

        builder.AppendLine("    }");
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

    private static string GenerateInterceptorProxiesSource(IReadOnlyList<DiscoveredInterceptedService> interceptedServices, string assemblyName)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        builder.AppendLine("// <auto-generated/>");
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
            GenerateInterceptorProxyClass(builder, service);
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
                builder.AppendLine($"        // Register interceptor: {interceptorType}");
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

    private static void GenerateInterceptorProxyClass(StringBuilder builder, DiscoveredInterceptedService service)
    {
        var proxyTypeName = GetProxyTypeName(service.TypeName);
        var shortTypeName = GetShortTypeName(service.TypeName);

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
            GenerateInterceptedMethod(builder, method, methodIndex, service.TypeName);
            methodIndex++;
        }

        builder.AppendLine("}");
    }

    private static void GenerateInterceptedMethod(StringBuilder builder, TypeDiscoveryHelper.InterceptedMethodInfo method, int methodIndex, string targetTypeName)
    {
        var parameterList = method.GetParameterList();
        var argumentList = method.GetArgumentList();

        builder.AppendLine($"    public {method.ReturnType} {method.Name}({parameterList})");
        builder.AppendLine("    {");

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

    private readonly struct DiscoveredDecorator
    {
        public DiscoveredDecorator(string decoratorTypeName, string serviceTypeName, int order, string assemblyName)
        {
            DecoratorTypeName = decoratorTypeName;
            ServiceTypeName = serviceTypeName;
            Order = order;
            AssemblyName = assemblyName;
        }

        public string DecoratorTypeName { get; }
        public string ServiceTypeName { get; }
        public int Order { get; }
        public string AssemblyName { get; }
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
            string[] allInterceptorTypeNames)
        {
            TypeName = typeName;
            InterfaceNames = interfaceNames;
            AssemblyName = assemblyName;
            Lifetime = lifetime;
            Methods = methods;
            AllInterceptorTypeNames = allInterceptorTypeNames;
        }

        public string TypeName { get; }
        public string[] InterfaceNames { get; }
        public string AssemblyName { get; }
        public GeneratorLifetime Lifetime { get; }
        public TypeDiscoveryHelper.InterceptedMethodInfo[] Methods { get; }
        public string[] AllInterceptorTypeNames { get; }
    }
}
