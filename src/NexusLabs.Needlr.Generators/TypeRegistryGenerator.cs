using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NexusLabs.Needlr.Generators.Helpers;
using NexusLabs.Needlr.Generators.Models;
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

            var bootstrapText = GenerateModuleInitializerBootstrapSource(assemblyName, referencedAssemblies, breadcrumbs, discoveryResult.Factories.Count > 0);
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

            // Generate factory classes if any were discovered
            if (discoveryResult.Factories.Count > 0)
            {
                var factoriesText = GenerateFactoriesSource(discoveryResult.Factories, assemblyName, breadcrumbs, projectDirectory);
                spc.AddSource("Factories.g.cs", SourceText.From(factoriesText, Encoding.UTF8));
            }

            // Generate options validator classes if any have [OptionsValidator] methods
            var optionsWithValidators = discoveryResult.Options.Where(o => o.HasValidatorMethod).ToList();
            if (optionsWithValidators.Count > 0)
            {
                var validatorsText = GenerateOptionsValidatorsSource(optionsWithValidators, assemblyName, breadcrumbs, projectDirectory);
                spc.AddSource("OptionsValidators.g.cs", SourceText.From(validatorsText, Encoding.UTF8));
            }

            // Generate diagnostic output files if configured
            var diagnosticOptions = GetDiagnosticOptions(configOptions);
            if (diagnosticOptions.Enabled)
            {
                var referencedAssemblyTypes = DiscoverReferencedAssemblyTypesForDiagnostics(compilation);
                var diagnosticsText = GenerateDiagnosticsSource(discoveryResult, assemblyName, projectDirectory, diagnosticOptions, referencedAssemblies, referencedAssemblyTypes);
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
        var openDecorators = new List<DiscoveredOpenDecorator>();
        var interceptedServices = new List<DiscoveredInterceptedService>();
        var factories = new List<DiscoveredFactory>();
        var options = new List<DiscoveredOptions>();
        var inaccessibleTypes = new List<InaccessibleType>();
        var prefixList = namespacePrefixes?.ToList();

        // Collect types from the current compilation if includeSelf is true
        if (includeSelf)
        {
            CollectTypesFromAssembly(compilation.Assembly, prefixList, injectableTypes, pluginTypes, hubRegistrations, kernelPlugins, decorators, openDecorators, interceptedServices, factories, options, inaccessibleTypes, compilation, isCurrentAssembly: true);
        }

        // Collect types from all referenced assemblies
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                CollectTypesFromAssembly(assemblySymbol, prefixList, injectableTypes, pluginTypes, hubRegistrations, kernelPlugins, decorators, openDecorators, interceptedServices, factories, options, inaccessibleTypes, compilation, isCurrentAssembly: false);
            }
        }

        // Expand open generic decorators into closed decorator registrations
        if (openDecorators.Count > 0)
        {
            ExpandOpenDecorators(injectableTypes, openDecorators, decorators);
        }

        // Filter out nested options types (types used as properties in other options types)
        if (options.Count > 1)
        {
            options = FilterNestedOptions(options, compilation);
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

        return new DiscoveryResult(injectableTypes, pluginTypes, hubRegistrations, kernelPlugins, decorators, inaccessibleTypes, missingTypeRegistryPlugins, interceptedServices, factories, options);
    }

    private static void CollectTypesFromAssembly(
        IAssemblySymbol assembly,
        IReadOnlyList<string>? namespacePrefixes,
        List<DiscoveredType> injectableTypes,
        List<DiscoveredPlugin> pluginTypes,
        List<DiscoveredHubRegistration> hubRegistrations,
        List<DiscoveredKernelPlugin> kernelPlugins,
        List<DiscoveredDecorator> decorators,
        List<DiscoveredOpenDecorator> openDecorators,
        List<DiscoveredInterceptedService> interceptedServices,
        List<DiscoveredFactory> factories,
        List<DiscoveredOptions> options,
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

            // Check for [Options] attribute
            if (TypeDiscoveryHelper.HasOptionsAttribute(typeSymbol))
            {
                var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                var optionsAttrs = TypeDiscoveryHelper.GetOptionsAttributes(typeSymbol);
                var sourceFilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;

                foreach (var optionsAttr in optionsAttrs)
                {
                    // Determine validator type and method
                    var validatorTypeSymbol = optionsAttr.ValidatorType;
                    var targetType = validatorTypeSymbol ?? typeSymbol; // Look for method on options class or external validator
                    var methodName = optionsAttr.ValidateMethod ?? "Validate"; // Convention: "Validate"

                    // Find validation method using convention-based discovery
                    var validatorMethodInfo = TypeDiscoveryHelper.FindValidationMethod(targetType, methodName);
                    OptionsValidatorInfo? validatorInfo = validatorMethodInfo.HasValue
                        ? new OptionsValidatorInfo(validatorMethodInfo.Value.MethodName, validatorMethodInfo.Value.IsStatic)
                        : null;

                    // Infer section name if not provided
                    var sectionName = optionsAttr.SectionName
                        ?? Helpers.OptionsNamingHelper.InferSectionName(typeSymbol.Name);

                    var validatorTypeName = validatorTypeSymbol != null
                        ? TypeDiscoveryHelper.GetFullyQualifiedName(validatorTypeSymbol)
                        : null;

                    options.Add(new DiscoveredOptions(
                        typeName,
                        sectionName,
                        optionsAttr.Name,
                        optionsAttr.ValidateOnStart,
                        assembly.Name,
                        sourceFilePath,
                        validatorInfo,
                        optionsAttr.ValidateMethod,
                        validatorTypeName));
                }
            }

            // Check for [GenerateFactory] attribute - these types get factories instead of direct registration
            if (TypeDiscoveryHelper.HasGenerateFactoryAttribute(typeSymbol))
            {
                var factoryConstructors = TypeDiscoveryHelper.GetFactoryConstructors(typeSymbol);
                if (factoryConstructors.Count > 0)
                {
                    // Has at least one constructor with runtime params - generate factory
                    var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                    var interfaces = TypeDiscoveryHelper.GetRegisterableInterfaces(typeSymbol);
                    var interfaceNames = interfaces.Select(i => TypeDiscoveryHelper.GetFullyQualifiedName(i)).ToArray();
                    var generationMode = TypeDiscoveryHelper.GetFactoryGenerationMode(typeSymbol);
                    var returnTypeOverride = TypeDiscoveryHelper.GetFactoryReturnInterfaceType(typeSymbol);
                    var sourceFilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;

                    factories.Add(new DiscoveredFactory(
                        typeName,
                        interfaceNames,
                        assembly.Name,
                        generationMode,
                        factoryConstructors.ToArray(),
                        returnTypeOverride,
                        sourceFilePath));
                    
                    continue; // Don't add to injectable types - factory handles registration
                }
                // If no runtime params, fall through to normal registration (with warning in future analyzer)
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

            // Check for OpenDecoratorFor attributes (source-gen only open generic decorators)
            var openDecoratorInfos = TypeDiscoveryHelper.GetOpenDecoratorForAttributes(typeSymbol);
            foreach (var openDecoratorInfo in openDecoratorInfos)
            {
                var sourceFilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;
                openDecorators.Add(new DiscoveredOpenDecorator(
                    openDecoratorInfo.DecoratorType,
                    openDecoratorInfo.OpenGenericInterface,
                    openDecoratorInfo.Order,
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

                    // Get [Keyed] attribute keys
                    var serviceKeys = TypeDiscoveryHelper.GetKeyedServiceKeys(typeSymbol);

                    injectableTypes.Add(new DiscoveredType(typeName, interfaceNames, assembly.Name, lifetime.Value, constructorParams, serviceKeys, sourceFilePath));
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
        var hasOptions = discoveryResult.Options.Count > 0;

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Type Registry");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        if (hasOptions)
        {
            builder.AppendLine("using Microsoft.Extensions.Configuration;");
        }
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

        if (hasOptions)
        {
            builder.AppendLine();
            GenerateRegisterOptionsMethod(builder, discoveryResult.Options, safeAssemblyName, breadcrumbs, projectDirectory);
        }

        builder.AppendLine();
        GenerateApplyDecoratorsMethod(builder, discoveryResult.Decorators, discoveryResult.InterceptedServices.Count > 0, safeAssemblyName, breadcrumbs, projectDirectory);

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string GenerateModuleInitializerBootstrapSource(string assemblyName, IReadOnlyList<string> referencedAssemblies, BreadcrumbWriter breadcrumbs, bool hasFactories)
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
        
        // Generate the decorator/factory applier lambda
        if (hasFactories)
        {
            builder.AppendLine("            services =>");
            builder.AppendLine("            {");
            builder.AppendLine($"                global::{safeAssemblyName}.Generated.TypeRegistry.ApplyDecorators((IServiceCollection)services);");
            builder.AppendLine($"                global::{safeAssemblyName}.Generated.FactoryRegistrations.RegisterFactories((IServiceCollection)services);");
            builder.AppendLine("            });");
        }
        else
        {
            builder.AppendLine($"            services => global::{safeAssemblyName}.Generated.TypeRegistry.ApplyDecorators((IServiceCollection)services));");
        }
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
                    var keysInfo = type.ServiceKeys.Length > 0
                        ? $"Keys: {string.Join(", ", type.ServiceKeys.Select(k => $"\"{k}\""))}"
                        : null;
                    
                    if (keysInfo != null)
                    {
                        breadcrumbs.WriteVerboseBox(builder, "        ",
                            $"{type.TypeName.Split('.').Last()} → {interfaces}",
                            $"Source: {sourcePath}",
                            $"Lifetime: {type.Lifetime}",
                            keysInfo);
                    }
                    else
                    {
                        breadcrumbs.WriteVerboseBox(builder, "        ",
                            $"{type.TypeName.Split('.').Last()} → {interfaces}",
                            $"Source: {sourcePath}",
                            $"Lifetime: {type.Lifetime}");
                    }
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
                builder.Append("), ");

                // Service keys from [Keyed] attributes
                if (type.ServiceKeys.Length == 0)
                {
                    builder.AppendLine("Array.Empty<string>()),");
                }
                else
                {
                    builder.Append("[");
                    builder.Append(string.Join(", ", type.ServiceKeys.Select(k => $"\"{EscapeStringLiteral(k)}\"")));
                    builder.AppendLine("]),");
                }
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

    private static void GenerateRegisterOptionsMethod(StringBuilder builder, IReadOnlyList<DiscoveredOptions> options, string safeAssemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Registers all discovered options types with the service collection.");
        builder.AppendLine("    /// This binds configuration sections to strongly-typed options classes.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <param name=\"services\">The service collection to configure.</param>");
        builder.AppendLine("    /// <param name=\"configuration\">The configuration root to bind options from.</param>");
        builder.AppendLine("    public static void RegisterOptions(IServiceCollection services, IConfiguration configuration)");
        builder.AppendLine("    {");

        if (options.Count == 0)
        {
            breadcrumbs.WriteInlineComment(builder, "        ", "No options types discovered");
        }
        else
        {
            foreach (var opt in options)
            {
                var typeName = opt.TypeName;
                
                if (opt.ValidateOnStart)
                {
                    // Use AddOptions pattern for validation support
                    // services.AddOptions<T>().BindConfiguration("Section").ValidateDataAnnotations().ValidateOnStart();
                    builder.Append($"        services.AddOptions<{typeName}>");
                    
                    if (opt.IsNamed)
                    {
                        builder.Append($"(\"{opt.Name}\")");
                    }
                    else
                    {
                        builder.Append("()");
                    }
                    
                    builder.Append($".BindConfiguration(\"{opt.SectionName}\")");
                    builder.Append(".ValidateDataAnnotations()");
                    builder.AppendLine(".ValidateOnStart();");

                    // If there's a custom validator method, register the generated validator
                    if (opt.HasValidatorMethod)
                    {
                        var shortTypeName = GetShortTypeName(typeName);
                        var validatorClassName = $"global::{safeAssemblyName}.Generated.{shortTypeName}Validator";
                        builder.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Options.IValidateOptions<{typeName}>, {validatorClassName}>();");
                    }
                }
                else if (opt.IsNamed)
                {
                    // Named options: OptionsConfigurationServiceCollectionExtensions.Configure<T>(services, "name", section)
                    builder.AppendLine($"        global::Microsoft.Extensions.DependencyInjection.OptionsConfigurationServiceCollectionExtensions.Configure<{typeName}>(services, \"{opt.Name}\", configuration.GetSection(\"{opt.SectionName}\"));");
                }
                else
                {
                    // Default options: OptionsConfigurationServiceCollectionExtensions.Configure<T>(services, section)
                    builder.AppendLine($"        global::Microsoft.Extensions.DependencyInjection.OptionsConfigurationServiceCollectionExtensions.Configure<{typeName}>(services, configuration.GetSection(\"{opt.SectionName}\"));");
                }
            }
        }

        builder.AppendLine("    }");
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

    private static string GenerateFactoriesSource(IReadOnlyList<DiscoveredFactory> factories, string assemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Generated Factories");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();

        // Generate factory interfaces and implementations for each type
        foreach (var factory in factories)
        {
            if (factory.GenerateInterface)
            {
                GenerateFactoryInterface(builder, factory, breadcrumbs, projectDirectory);
                builder.AppendLine();
                GenerateFactoryImplementation(builder, factory, breadcrumbs, projectDirectory);
                builder.AppendLine();
            }
        }

        // Generate the registration helper
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Helper class for registering factory types.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine("public static class FactoryRegistrations");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Registers all generated factories.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <param name=\"services\">The service collection to register to.</param>");
        builder.AppendLine("    public static void RegisterFactories(IServiceCollection services)");
        builder.AppendLine("    {");

        foreach (var factory in factories)
        {
            breadcrumbs.WriteInlineComment(builder, "        ", $"Factory for {factory.SimpleTypeName}");

            // Register Func<> for each constructor
            if (factory.GenerateFunc)
            {
                foreach (var ctor in factory.Constructors)
                {
                    GenerateFuncRegistration(builder, factory, ctor, "        ");
                }
            }

            // Register interface factory
            if (factory.GenerateInterface)
            {
                var factoryInterfaceName = $"I{factory.SimpleTypeName}Factory";
                var factoryImplName = $"{factory.SimpleTypeName}Factory";
                builder.AppendLine($"        services.AddSingleton<global::{safeAssemblyName}.Generated.{factoryInterfaceName}, global::{safeAssemblyName}.Generated.{factoryImplName}>();");
            }
        }

        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Gets the number of factory types generated at compile time.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine($"    public static int Count => {factories.Count};");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string GenerateOptionsValidatorsSource(IReadOnlyList<DiscoveredOptions> optionsWithValidators, string assemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Generated Options Validators");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using System.Linq;");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.Extensions.Options;");
        builder.AppendLine();
        builder.AppendLine("using NexusLabs.Needlr.Generators;");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();

        // Generate validator class for each options type with a validator method
        foreach (var opt in optionsWithValidators)
        {
            if (!opt.HasValidatorMethod || opt.ValidatorMethod == null)
                continue;

            var shortTypeName = GetShortTypeName(opt.TypeName);
            var validatorClassName = shortTypeName + "Validator";

            // Determine which type has the validator method
            var validatorTargetType = opt.HasExternalValidator ? opt.ValidatorTypeName! : opt.TypeName;

            builder.AppendLine("/// <summary>");
            builder.AppendLine($"/// Generated validator for <see cref=\"{opt.TypeName}\"/>.");
            if (opt.HasExternalValidator)
            {
                builder.AppendLine($"/// Uses external validator <see cref=\"{validatorTargetType}\"/>.");
            }
            else
            {
                builder.AppendLine("/// Calls the validation method on the options instance.");
            }
            builder.AppendLine("/// </summary>");
            builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
            builder.AppendLine($"public sealed class {validatorClassName} : IValidateOptions<{opt.TypeName}>");
            builder.AppendLine("{");

            if (opt.HasExternalValidator && !opt.ValidatorMethod.Value.IsStatic)
            {
                // External validator needs to be injected for instance methods
                builder.AppendLine($"    private readonly {validatorTargetType} _validator;");
                builder.AppendLine();
                builder.AppendLine($"    public {validatorClassName}({validatorTargetType} validator)");
                builder.AppendLine("    {");
                builder.AppendLine("        _validator = validator;");
                builder.AppendLine("    }");
                builder.AppendLine();
            }

            builder.AppendLine($"    public ValidateOptionsResult Validate(string? name, {opt.TypeName} options)");
            builder.AppendLine("    {");
            builder.AppendLine("        var errors = new List<string>();");

            // Generate the foreach to iterate errors
            string validationCall;
            if (opt.HasExternalValidator)
            {
                if (opt.ValidatorMethod.Value.IsStatic)
                {
                    // Static method on external type: ExternalValidator.ValidateMethod(options)
                    validationCall = $"{validatorTargetType}.{opt.ValidatorMethod.Value.MethodName}(options)";
                }
                else
                {
                    // Instance method on external type: _validator.ValidateMethod(options)
                    validationCall = $"_validator.{opt.ValidatorMethod.Value.MethodName}(options)";
                }
            }
            else if (opt.ValidatorMethod.Value.IsStatic)
            {
                // Static method on options type: OptionsType.ValidateMethod(options)
                validationCall = $"{opt.TypeName}.{opt.ValidatorMethod.Value.MethodName}(options)";
            }
            else
            {
                // Instance method on options type: options.ValidateMethod()
                validationCall = $"options.{opt.ValidatorMethod.Value.MethodName}()";
            }

            builder.AppendLine($"        foreach (var error in {validationCall})");
            builder.AppendLine("        {");
            builder.AppendLine("            // Support both string and ValidationError (ValidationError.ToString() returns formatted message)");
            builder.AppendLine("            var errorMessage = error?.ToString() ?? string.Empty;");
            builder.AppendLine("            if (!string.IsNullOrEmpty(errorMessage))");
            builder.AppendLine("            {");
            builder.AppendLine("                errors.Add(errorMessage);");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        if (errors.Count > 0)");
            builder.AppendLine("        {");
            builder.AppendLine($"            return ValidateOptionsResult.Fail(errors);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        return ValidateOptionsResult.Success;");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void GenerateFactoryInterface(StringBuilder builder, DiscoveredFactory factory, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var factoryName = $"I{factory.SimpleTypeName}Factory";

        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Factory interface for creating instances of <see cref=\"{factory.TypeName}\"/>.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine($"public interface {factoryName}");
        builder.AppendLine("{");

        // Generate Create method for each constructor
        foreach (var ctor in factory.Constructors)
        {
            var runtimeParamList = string.Join(", ", ctor.RuntimeParameters.Select(p => 
            {
                var simpleTypeName = GetSimpleTypeName(p.TypeName);
                return $"{p.TypeName} {p.ParameterName ?? ToCamelCase(simpleTypeName)}";
            }));

            builder.AppendLine($"    /// <summary>Creates a new instance of {factory.SimpleTypeName}.</summary>");
            
            // Add <param> tags for documented runtime parameters
            foreach (var param in ctor.RuntimeParameters)
            {
                if (!string.IsNullOrWhiteSpace(param.DocumentationComment))
                {
                    var paramName = param.ParameterName ?? ToCamelCase(GetSimpleTypeName(param.TypeName));
                    var escapedDoc = EscapeXmlContent(param.DocumentationComment!);
                    builder.AppendLine($"    /// <param name=\"{paramName}\">{escapedDoc}</param>");
                }
            }
            
            builder.AppendLine($"    {factory.ReturnTypeName} Create({runtimeParamList});");
        }

        builder.AppendLine("}");
    }

    /// <summary>
    /// Escapes special XML characters in documentation content.
    /// </summary>
    private static string EscapeXmlContent(string content)
    {
        // The content from GetDocumentationCommentXml() is already parsed,
        // so entities like &lt; are already decoded. We need to re-encode them.
        return content
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static void GenerateFactoryImplementation(StringBuilder builder, DiscoveredFactory factory, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var factoryInterfaceName = $"I{factory.SimpleTypeName}Factory";
        var factoryImplName = $"{factory.SimpleTypeName}Factory";

        // Collect all unique injectable parameters across all constructors
        var allInjectableParams = factory.Constructors
            .SelectMany(c => c.InjectableParameters)
            .GroupBy(p => p.TypeName)
            .Select(g => g.First())
            .ToList();

        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Factory implementation for creating instances of <see cref=\"{factory.TypeName}\"/>.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine($"internal sealed class {factoryImplName} : {factoryInterfaceName}");
        builder.AppendLine("{");

        // Fields for injectable dependencies
        foreach (var param in allInjectableParams)
        {
            var fieldName = "_" + ToCamelCase(GetSimpleTypeName(param.TypeName));
            builder.AppendLine($"    private readonly {param.TypeName} {fieldName};");
        }

        builder.AppendLine();

        // Constructor
        var ctorParams = string.Join(", ", allInjectableParams.Select(p => $"{p.TypeName} {ToCamelCase(GetSimpleTypeName(p.TypeName))}"));
        builder.AppendLine($"    public {factoryImplName}({ctorParams})");
        builder.AppendLine("    {");
        foreach (var param in allInjectableParams)
        {
            var fieldName = "_" + ToCamelCase(GetSimpleTypeName(param.TypeName));
            var paramName = ToCamelCase(GetSimpleTypeName(param.TypeName));
            builder.AppendLine($"        {fieldName} = {paramName};");
        }
        builder.AppendLine("    }");
        builder.AppendLine();

        // Create methods for each constructor
        foreach (var ctor in factory.Constructors)
        {
            var runtimeParamList = string.Join(", ", ctor.RuntimeParameters.Select(p => 
            {
                var paramName = p.ParameterName ?? ToCamelCase(GetSimpleTypeName(p.TypeName));
                return $"{p.TypeName} {paramName}";
            }));

            builder.AppendLine($"    public {factory.ReturnTypeName} Create({runtimeParamList})");
            builder.AppendLine("    {");
            builder.Append($"        return new {factory.TypeName}(");

            // Build constructor arguments - injectable first (from fields), then runtime
            var allArgs = new List<string>();
            foreach (var inj in ctor.InjectableParameters)
            {
                var fieldName = "_" + ToCamelCase(GetSimpleTypeName(inj.TypeName));
                allArgs.Add(fieldName);
            }
            foreach (var rt in ctor.RuntimeParameters)
            {
                var paramName = rt.ParameterName ?? ToCamelCase(GetSimpleTypeName(rt.TypeName));
                allArgs.Add(paramName);
            }

            builder.Append(string.Join(", ", allArgs));
            builder.AppendLine(");");
            builder.AppendLine("    }");
        }

        builder.AppendLine("}");
    }

    private static void GenerateFuncRegistration(StringBuilder builder, DiscoveredFactory factory, TypeDiscoveryHelper.FactoryConstructorInfo ctor, string indent)
    {
        // Build Func<TRuntime..., TReturn> type - uses ReturnTypeName (interface if generic attribute used)
        var runtimeTypes = string.Join(", ", ctor.RuntimeParameters.Select(p => p.TypeName));
        var funcType = $"Func<{runtimeTypes}, {factory.ReturnTypeName}>";

        // Build the lambda
        var runtimeParams = string.Join(", ", ctor.RuntimeParameters.Select(p => 
            p.ParameterName ?? ToCamelCase(GetSimpleTypeName(p.TypeName))));

        builder.AppendLine($"{indent}services.AddSingleton<{funcType}>(sp =>");
        builder.AppendLine($"{indent}    ({runtimeParams}) => new {factory.TypeName}(");

        // Build constructor call arguments
        var allArgs = new List<string>();
        foreach (var inj in ctor.InjectableParameters)
        {
            if (inj.IsKeyed)
            {
                allArgs.Add($"sp.GetRequiredKeyedService<{inj.TypeName}>(\"{EscapeStringLiteral(inj.ServiceKey!)}\")");
            }
            else
            {
                allArgs.Add($"sp.GetRequiredService<{inj.TypeName}>()");
            }
        }
        foreach (var rt in ctor.RuntimeParameters)
        {
            allArgs.Add(rt.ParameterName ?? ToCamelCase(GetSimpleTypeName(rt.TypeName)));
        }

        for (int i = 0; i < allArgs.Count; i++)
        {
            var arg = allArgs[i];
            var isLast = i == allArgs.Count - 1;
            builder.AppendLine($"{indent}        {arg}{(isLast ? ")" : ",")}");
        }
        builder.AppendLine($"{indent});");
    }

    private static string GetSimpleTypeName(string fullyQualifiedName)
    {
        // "global::System.String" -> "String"
        var parts = fullyQualifiedName.Split('.');
        return parts[parts.Length - 1];
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        
        // Remove leading 'I' for interfaces
        if (name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
            name = name.Substring(1);
        
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static string GenerateDiagnosticsSource(DiscoveryResult discoveryResult, string assemblyName, string? projectDirectory, DiagnosticOptions options, IReadOnlyList<string> referencedTypeRegistryAssemblies, Dictionary<string, List<DiagnosticTypeInfo>> referencedAssemblyTypes)
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
        var dependencyGraphContent = GenerateDependencyGraphMarkdown(discoveryResult, assemblyName, timestamp, options.TypeFilter, referencedTypeRegistryAssemblies, referencedAssemblyTypes);
        builder.AppendLine("    /// <summary>DependencyGraph.md content</summary>");
        builder.AppendLine($"    public const string DependencyGraph = @\"{EscapeVerbatimStringLiteral(dependencyGraphContent)}\";");
        builder.AppendLine();

        var lifetimeSummaryContent = GenerateLifetimeSummaryMarkdown(discoveryResult, assemblyName, timestamp, options.TypeFilter, referencedAssemblyTypes);
        builder.AppendLine("    /// <summary>LifetimeSummary.md content</summary>");
        builder.AppendLine($"    public const string LifetimeSummary = @\"{EscapeVerbatimStringLiteral(lifetimeSummaryContent)}\";");
        builder.AppendLine();

        var registrationIndexContent = GenerateRegistrationIndexMarkdown(discoveryResult, assemblyName, projectDirectory, timestamp, options.TypeFilter, referencedAssemblyTypes);
        builder.AppendLine("    /// <summary>RegistrationIndex.md content</summary>");
        builder.AppendLine($"    public const string RegistrationIndex = @\"{EscapeVerbatimStringLiteral(registrationIndexContent)}\";");
        builder.AppendLine();

        var analyzerStatusContent = GenerateAnalyzerStatusMarkdown(timestamp);
        builder.AppendLine("    /// <summary>AnalyzerStatus.md content</summary>");
        builder.AppendLine($"    public const string AnalyzerStatus = @\"{EscapeVerbatimStringLiteral(analyzerStatusContent)}\";");

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string GenerateDependencyGraphMarkdown(DiscoveryResult discovery, string assemblyName, string timestamp, HashSet<string> typeFilter, IReadOnlyList<string> referencedTypeRegistryAssemblies, Dictionary<string, List<DiagnosticTypeInfo>> referencedAssemblyTypes)
    {
        var sb = new StringBuilder();
        var types = FilterTypes(discovery.InjectableTypes, typeFilter);

        sb.AppendLine("# Needlr Dependency Graph");
        sb.AppendLine();
        sb.AppendLine($"Generated: {timestamp} UTC");
        sb.AppendLine($"Assembly: {assemblyName}");
        sb.AppendLine();

        // Show referenced TypeRegistry assemblies with their types
        if (referencedAssemblyTypes.Count > 0)
        {
            sb.AppendLine("## Referenced Plugin Assemblies");
            sb.AppendLine();
            sb.AppendLine("Types from referenced assemblies with `[GenerateTypeRegistry]`:");
            sb.AppendLine();

            foreach (var kvp in referencedAssemblyTypes.OrderBy(kv => kv.Key))
            {
                var refAsm = kvp.Key;
                var refTypes = kvp.Value;
                
                sb.AppendLine($"### {refAsm}");
                sb.AppendLine();
                sb.AppendLine("```mermaid");
                sb.AppendLine("graph TD");

                // Group by lifetime
                var refSingletons = refTypes.Where(t => t.Lifetime == GeneratorLifetime.Singleton).ToList();
                var refScopeds = refTypes.Where(t => t.Lifetime == GeneratorLifetime.Scoped).ToList();
                var refTransients = refTypes.Where(t => t.Lifetime == GeneratorLifetime.Transient).ToList();

                if (refSingletons.Any())
                {
                    sb.AppendLine($"    subgraph Singleton[\"{refAsm} - Singleton\"]");
                    foreach (var type in refSingletons)
                    {
                        var shape = type.IsDecorator ? "[[" : (type.HasFactory ? "{{" : "[");
                        var endShape = type.IsDecorator ? "]]" : (type.HasFactory ? "}}" : "]");
                        sb.AppendLine($"        {GetMermaidNodeId(type.FullName)}{shape}\"{type.ShortName}\"{endShape}");
                    }
                    sb.AppendLine("    end");
                }
                if (refScopeds.Any())
                {
                    sb.AppendLine($"    subgraph Scoped[\"{refAsm} - Scoped\"]");
                    foreach (var type in refScopeds)
                    {
                        var shape = type.IsDecorator ? "[[" : (type.HasFactory ? "{{" : "[");
                        var endShape = type.IsDecorator ? "]]" : (type.HasFactory ? "}}" : "]");
                        sb.AppendLine($"        {GetMermaidNodeId(type.FullName)}{shape}\"{type.ShortName}\"{endShape}");
                    }
                    sb.AppendLine("    end");
                }
                if (refTransients.Any())
                {
                    sb.AppendLine($"    subgraph Transient[\"{refAsm} - Transient\"]");
                    foreach (var type in refTransients)
                    {
                        var shape = type.IsDecorator ? "[[" : (type.HasFactory ? "{{" : "[");
                        var endShape = type.IsDecorator ? "]]" : (type.HasFactory ? "}}" : "]");
                        sb.AppendLine($"        {GetMermaidNodeId(type.FullName)}{shape}\"{type.ShortName}\"{endShape}");
                    }
                    sb.AppendLine("    end");
                }

                // Show dependency edges within this assembly
                var refTypeNames = new HashSet<string>(refTypes.Select(t => t.ShortName), StringComparer.Ordinal);
                foreach (var type in refTypes)
                {
                    foreach (var dep in type.Dependencies)
                    {
                        var depShort = GetShortTypeName(dep);
                        var matchingType = refTypes.FirstOrDefault(t =>
                            t.ShortName == depShort ||
                            t.Interfaces.Any(i => GetShortTypeName(i) == depShort));
                        if (matchingType.FullName != null)
                        {
                            sb.AppendLine($"    {GetMermaidNodeId(type.FullName)} --> {GetMermaidNodeId(matchingType.FullName)}");
                        }
                    }
                }

                // Show factory→product edges (dotted arrows)
                var factorySources = refTypes.Where(t => t.HasFactory).ToList();
                foreach (var source in factorySources)
                {
                    var factoryName = source.ShortName + "Factory";
                    var matchingFactory = refTypes.FirstOrDefault(t => t.ShortName == factoryName);
                    if (matchingFactory.FullName != null)
                    {
                        sb.AppendLine($"    {GetMermaidNodeId(matchingFactory.FullName)} -.->|produces| {GetMermaidNodeId(source.FullName)}");
                    }
                }

                sb.AppendLine("```");
                sb.AppendLine();

                // Show summary table for this assembly
                sb.AppendLine($"| Service | Lifetime | Interfaces |");
                sb.AppendLine($"|---------|----------|------------|");
                foreach (var type in refTypes.OrderBy(t => t.ShortName))
                {
                    var interfaces = type.Interfaces.Any() ? string.Join(", ", type.Interfaces.Select(GetShortTypeName)) : "-";
                    sb.AppendLine($"| {type.ShortName} | {type.Lifetime} | {interfaces} |");
                }
                sb.AppendLine();
            }
        }

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

        // Decorator chains section (aggregates host and plugin decorators)
        var pluginDecorators = referencedAssemblyTypes
            .SelectMany(kv => kv.Value.Where(t => t.IsDecorator).Select(t => (Assembly: kv.Key, Type: t)))
            .ToList();
        
        if (discovery.Decorators.Any() || pluginDecorators.Any())
        {
            sb.AppendLine("## Decorator Chains");
            sb.AppendLine();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph LR");

            // Host decorators - group by service type
            var decoratorsByService = discovery.Decorators
                .GroupBy(d => d.ServiceTypeName)
                .OrderBy(g => g.Key);

            foreach (var serviceGroup in decoratorsByService)
            {
                var serviceShortName = GetShortTypeName(serviceGroup.Key);
                var orderedDecorators = serviceGroup.OrderByDescending(d => d.Order).ToList();

                // Find the underlying implementation for this service
                var implementation = types.FirstOrDefault(t =>
                    t.InterfaceNames.Any(i => GetShortTypeName(i) == serviceShortName) &&
                    !discovery.Decorators.Any(d => GetShortTypeName(d.DecoratorTypeName) == GetShortTypeName(t.TypeName)));

                // Build the chain: highest order decorator -> ... -> lowest order decorator -> implementation
                for (int i = 0; i < orderedDecorators.Count; i++)
                {
                    var decorator = orderedDecorators[i];
                    var decoratorId = GetMermaidNodeId(decorator.DecoratorTypeName);
                    var decoratorName = GetShortTypeName(decorator.DecoratorTypeName);

                    // Add node definition
                    sb.AppendLine($"    {decoratorId}[[\"{decoratorName}\"]]");

                    // Add edge to next decorator or implementation
                    if (i < orderedDecorators.Count - 1)
                    {
                        var nextDecorator = orderedDecorators[i + 1];
                        sb.AppendLine($"    {decoratorId} --> {GetMermaidNodeId(nextDecorator.DecoratorTypeName)}");
                    }
                    else if (implementation.TypeName != null)
                    {
                        var implId = GetMermaidNodeId(implementation.TypeName);
                        var implName = GetShortTypeName(implementation.TypeName);
                        sb.AppendLine($"    {implId}[\"{implName}\"]");
                        sb.AppendLine($"    {decoratorId} --> {implId}");
                    }
                }
            }

            // Plugin decorators (we don't have chain order info, just show the decorator types)
            foreach (var (assembly, type) in pluginDecorators.OrderBy(x => x.Type.ShortName))
            {
                var decoratorId = GetMermaidNodeId(type.FullName);
                var decoratorName = type.ShortName;
                sb.AppendLine($"    {decoratorId}[[\"{decoratorName}\"]]");
            }

            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Intercepted services section (aggregates host and plugin interceptors)
        var interceptedServices = discovery.InterceptedServices.ToList();
        var pluginInterceptors = referencedAssemblyTypes
            .SelectMany(kv => kv.Value.Where(t => t.HasInterceptorProxy).Select(t => (Assembly: kv.Key, Type: t)))
            .ToList();
        
        if (interceptedServices.Any() || pluginInterceptors.Any())
        {
            sb.AppendLine("## Intercepted Services");
            sb.AppendLine();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph LR");

            // Host intercepted services
            foreach (var service in interceptedServices.OrderBy(s => s.TypeName))
            {
                var targetId = GetMermaidNodeId(service.TypeName);
                var targetName = GetShortTypeName(service.TypeName);
                var proxyId = targetId + "_Proxy";
                var proxyName = targetName + "_InterceptorProxy";

                // Target service
                sb.AppendLine($"    {targetId}[\"{targetName}\"]");
                // Proxy with stadium shape
                sb.AppendLine($"    {proxyId}[[\"{proxyName}\"]]");
                // Edge showing proxy wraps target
                sb.AppendLine($"    {proxyId} -.->|wraps| {targetId}");
                
                // Show interceptors applied
                foreach (var interceptorType in service.AllInterceptorTypeNames)
                {
                    var interceptorId = GetMermaidNodeId(interceptorType);
                    var interceptorName = GetShortTypeName(interceptorType);
                    sb.AppendLine($"    {interceptorId}([[\"{interceptorName}\"]])");
                    sb.AppendLine($"    {proxyId} --> {interceptorId}");
                }
            }

            // Plugin intercepted services
            foreach (var (assembly, type) in pluginInterceptors.OrderBy(x => x.Type.ShortName))
            {
                var targetId = GetMermaidNodeId(type.FullName);
                var targetName = type.ShortName;
                var proxyId = targetId + "_Proxy";
                var proxyName = targetName + "_InterceptorProxy";

                sb.AppendLine($"    {targetId}[\"{targetName}\"]");
                sb.AppendLine($"    {proxyId}[[\"{proxyName}\"]]");
                sb.AppendLine($"    {proxyId} -.->|wraps| {targetId}");
            }

            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Keyed services section
        var keyedTypes = types.Where(t => t.IsKeyed).ToList();
        if (keyedTypes.Any())
        {
            sb.AppendLine("## Keyed Services");
            sb.AppendLine();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph TD");

            // Group by service key
            var typesByKey = keyedTypes
                .SelectMany(t => t.ServiceKeys.Select(k => (Key: k, Type: t)))
                .GroupBy(x => x.Key)
                .OrderBy(g => g.Key);

            foreach (var keyGroup in typesByKey)
            {
                var safeKey = SanitizeIdentifier(keyGroup.Key);
                sb.AppendLine($"    subgraph key_{safeKey}[\"{keyGroup.Key}\"]");
                foreach (var item in keyGroup.OrderBy(x => x.Type.TypeName))
                {
                    var nodeId = GetMermaidNodeId(item.Type.TypeName);
                    var nodeName = GetShortTypeName(item.Type.TypeName);
                    sb.AppendLine($"        {nodeId}[\"{nodeName}\"]");
                }
                sb.AppendLine("    end");
            }

            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Plugin assemblies section
        var plugins = FilterPluginTypes(discovery.PluginTypes, typeFilter);
        if (plugins.Any())
        {
            sb.AppendLine("## Plugin Assemblies");
            sb.AppendLine();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph TD");

            // Group plugins by assembly
            var pluginsByAssembly = plugins
                .GroupBy(p => p.AssemblyName)
                .OrderBy(g => g.Key);

            foreach (var asmGroup in pluginsByAssembly)
            {
                var safeAsm = SanitizeIdentifier(asmGroup.Key);
                var shortAsm = GetShortTypeName(asmGroup.Key);
                sb.AppendLine($"    subgraph asm_{safeAsm}[\"{shortAsm}\"]");
                foreach (var plugin in asmGroup.OrderBy(p => p.TypeName))
                {
                    var nodeId = GetMermaidNodeId(plugin.TypeName);
                    var nodeName = GetShortTypeName(plugin.TypeName);
                    // Use stadium shape for plugins
                    sb.AppendLine($"        {nodeId}([\"{nodeName}\"])");
                }
                sb.AppendLine("    end");
            }

            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Factory services section (aggregates host and plugin factories)
        var factories = FilterFactories(discovery.Factories, typeFilter);
        var pluginFactories = referencedAssemblyTypes
            .SelectMany(kv => kv.Value.Where(t => t.HasFactory).Select(t => (Assembly: kv.Key, Type: t)))
            .ToList();
        
        if (factories.Any() || pluginFactories.Any())
        {
            sb.AppendLine("## Factory Services");
            sb.AppendLine();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph LR");

            // Host factories
            foreach (var factory in factories.OrderBy(f => f.TypeName))
            {
                var sourceNodeId = GetMermaidNodeId(factory.TypeName);
                var sourceName = GetShortTypeName(factory.TypeName);
                var factoryNodeId = sourceNodeId + "Factory";
                var factoryName = sourceName + "Factory";

                // Generated factory with regular shape
                sb.AppendLine($"    {factoryNodeId}[\"{factoryName}\"]");
                // Source type (product) with hexagon shape
                sb.AppendLine($"    {sourceNodeId}{{{{\"{sourceName}\"}}}}");
                // Edge showing factory produces the type (dotted arrow)
                sb.AppendLine($"    {factoryNodeId} -.->|produces| {sourceNodeId}");
            }

            // Plugin factories
            foreach (var (assembly, type) in pluginFactories.OrderBy(f => f.Type.ShortName))
            {
                var sourceNodeId = GetMermaidNodeId(type.FullName);
                var sourceName = type.ShortName;
                var factoryNodeId = sourceNodeId + "Factory";
                var factoryName = sourceName + "Factory";

                // Generated factory with regular shape
                sb.AppendLine($"    {factoryNodeId}[\"{factoryName}\"]");
                // Source type (product) with hexagon shape
                sb.AppendLine($"    {sourceNodeId}{{{{\"{sourceName}\"}}}}");
                // Edge showing factory produces the type (dotted arrow)
                sb.AppendLine($"    {factoryNodeId} -.->|produces| {sourceNodeId}");
            }

            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Interface mapping section
        var typesWithInterfaces = types.Where(t => t.InterfaceNames.Length > 0).ToList();
        if (typesWithInterfaces.Any())
        {
            sb.AppendLine("## Interface Mapping");
            sb.AppendLine();
            sb.AppendLine("```mermaid");
            sb.AppendLine("graph LR");

            foreach (var type in typesWithInterfaces.OrderBy(t => t.TypeName))
            {
                var implId = GetMermaidNodeId(type.TypeName);
                var implName = GetShortTypeName(type.TypeName);
                sb.AppendLine($"    {implId}[\"{implName}\"]");

                foreach (var iface in type.InterfaceNames)
                {
                    var ifaceId = GetMermaidNodeId(iface);
                    var ifaceName = GetShortTypeName(iface);
                    // Interface uses rounded box, dotted edge points from interface to impl
                    sb.AppendLine($"    {ifaceId}((\"{ifaceName}\"))");
                    sb.AppendLine($"    {ifaceId} -.-> {implId}");
                }
            }

            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Complexity metrics section
        sb.AppendLine("## Complexity Metrics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");

        sb.AppendLine($"| Total Services | {types.Count} |");

        // Calculate max dependency depth using BFS
        var maxDepth = CalculateMaxDependencyDepth(types);
        sb.AppendLine($"| Max Dependency Depth | {maxDepth} |");

        // Calculate hub services (services that appear as dependencies in 3+ other services)
        var hubServices = CalculateHubServices(types, 3);
        sb.AppendLine($"| Hub Services (≥3 dependents) | {hubServices.Count} |");

        if (hubServices.Any())
        {
            sb.AppendLine();
            sb.AppendLine("**Hub Services:** " + string.Join(", ", hubServices.Select(h => $"{GetShortTypeName(h.TypeName)} ({h.DependentCount})")));
        }

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

    private static string GenerateLifetimeSummaryMarkdown(DiscoveryResult discovery, string assemblyName, string timestamp, HashSet<string> typeFilter, Dictionary<string, List<DiagnosticTypeInfo>> referencedAssemblyTypes)
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

        // Referenced plugin assemblies lifetime breakdown
        if (referencedAssemblyTypes.Count > 0)
        {
            sb.AppendLine("## Referenced Plugin Assemblies");
            sb.AppendLine();

            foreach (var kvp in referencedAssemblyTypes.OrderBy(kv => kv.Key))
            {
                var refAsm = kvp.Key;
                var refTypes = kvp.Value;
                var refSingletons = refTypes.Count(t => t.Lifetime == GeneratorLifetime.Singleton);
                var refScopeds = refTypes.Count(t => t.Lifetime == GeneratorLifetime.Scoped);
                var refTransients = refTypes.Count(t => t.Lifetime == GeneratorLifetime.Transient);
                var refTotal = refTypes.Count;

                sb.AppendLine($"### {refAsm}");
                sb.AppendLine();
                sb.AppendLine("| Lifetime | Count | % |");
                sb.AppendLine("|----------|-------|---|");
                if (refTotal > 0)
                {
                    sb.AppendLine($"| Singleton | {refSingletons} | {Percentage(refSingletons, refTotal)}% |");
                    sb.AppendLine($"| Scoped | {refScopeds} | {Percentage(refScopeds, refTotal)}% |");
                    sb.AppendLine($"| Transient | {refTransients} | {Percentage(refTransients, refTotal)}% |");
                    sb.AppendLine($"| **Total** | **{refTotal}** | 100% |");
                }
                sb.AppendLine();
            }
        }

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

    private static string GenerateRegistrationIndexMarkdown(DiscoveryResult discovery, string assemblyName, string? projectDirectory, string timestamp, HashSet<string> typeFilter, Dictionary<string, List<DiagnosticTypeInfo>> referencedAssemblyTypes)
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

        // Referenced plugin assemblies services
        if (referencedAssemblyTypes.Count > 0)
        {
            sb.AppendLine("## Referenced Plugin Assemblies");
            sb.AppendLine();

            foreach (var kvp in referencedAssemblyTypes.OrderBy(kv => kv.Key))
            {
                var refAsm = kvp.Key;
                var refTypes = kvp.Value;

                sb.AppendLine($"### {refAsm} ({refTypes.Count} services)");
                sb.AppendLine();
                sb.AppendLine("| # | Interface | Implementation | Lifetime |");
                sb.AppendLine("|---|-----------|----------------|----------|");

                var index = 1;
                foreach (var type in refTypes.OrderBy(t => t.ShortName))
                {
                    var iface = type.Interfaces.FirstOrDefault() ?? "-";
                    sb.AppendLine($"| {index} | {GetShortTypeName(iface)} | {type.ShortName} | {type.Lifetime} |");
                    index++;
                }
                sb.AppendLine();
            }
        }

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

        // Decorators section (aggregates host and plugin decorators)
        var pluginDecorators = referencedAssemblyTypes
            .SelectMany(kv => kv.Value.Where(t => t.IsDecorator).Select(t => (Assembly: kv.Key, Type: t)))
            .ToList();
        
        if (decorators.Any() || pluginDecorators.Any())
        {
            var totalCount = decorators.Count + pluginDecorators.Count;
            sb.AppendLine($"## Decorators ({totalCount})");
            sb.AppendLine();
            sb.AppendLine("| Service | Decorator Chain | Assembly |");
            sb.AppendLine("|---------|-----------------|----------|");

            // Host decorators
            var decoratorsByTarget = decorators
                .GroupBy(d => d.ServiceTypeName)
                .OrderBy(g => g.Key);

            foreach (var group in decoratorsByTarget)
            {
                var chain = string.Join(" → ",
                    group.OrderBy(d => d.Order)
                         .Select(d => GetShortTypeName(d.DecoratorTypeName)));
                sb.AppendLine($"| {GetShortTypeName(group.Key)} | {chain} | (host) |");
            }

            // Plugin decorators
            foreach (var (assembly, type) in pluginDecorators.OrderBy(x => x.Type.ShortName))
            {
                // For plugin decorators, we show them individually since we don't have chain info
                var serviceName = type.Interfaces.FirstOrDefault() ?? "-";
                sb.AppendLine($"| {GetShortTypeName(serviceName)} | {type.ShortName} | {assembly} |");
            }
            sb.AppendLine();
        }

        // Interceptors section (aggregates host and plugin interceptors)
        var interceptedServices = discovery.InterceptedServices.ToList();
        var pluginIntercepted = referencedAssemblyTypes
            .SelectMany(kv => kv.Value.Where(t => t.HasInterceptorProxy).Select(t => (Assembly: kv.Key, Type: t)))
            .ToList();
        
        if (interceptedServices.Any() || pluginIntercepted.Any())
        {
            var totalCount = interceptedServices.Count + pluginIntercepted.Count;
            sb.AppendLine($"## Intercepted Services ({totalCount})");
            sb.AppendLine();
            sb.AppendLine("| Service | Interceptors | Proxy | Assembly |");
            sb.AppendLine("|---------|--------------|-------|----------|");

            // Host intercepted services
            foreach (var service in interceptedServices.OrderBy(s => s.TypeName))
            {
                var serviceName = GetShortTypeName(service.TypeName);
                var interceptors = string.Join(", ", service.AllInterceptorTypeNames.Select(GetShortTypeName));
                var proxyName = serviceName + "_InterceptorProxy";
                sb.AppendLine($"| {serviceName} | {interceptors} | {proxyName} | (host) |");
            }

            // Plugin intercepted services
            foreach (var (assembly, type) in pluginIntercepted.OrderBy(x => x.Type.ShortName))
            {
                var proxyName = type.ShortName + "_InterceptorProxy";
                sb.AppendLine($"| {type.ShortName} | (see plugin) | {proxyName} | {assembly} |");
            }
            sb.AppendLine();
        }

        // Factories section (aggregates host and plugin factories)
        var factories = discovery.Factories.ToList();
        var pluginFactories = referencedAssemblyTypes
            .SelectMany(kv => kv.Value.Where(t => t.HasFactory).Select(t => (Assembly: kv.Key, Type: t)))
            .ToList();
        
        if (factories.Any() || pluginFactories.Any())
        {
            var totalCount = factories.Count + pluginFactories.Count;
            sb.AppendLine($"## Factories ({totalCount})");
            sb.AppendLine();
            sb.AppendLine("| Source Type | Factory Interface | Generated Factory | Assembly |");
            sb.AppendLine("|-------------|-------------------|-------------------|----------|");

            // Host factories
            foreach (var factory in factories.OrderBy(f => f.TypeName))
            {
                var sourceName = factory.SimpleTypeName;
                var factoryInterface = "I" + sourceName + "Factory";
                var factoryImpl = sourceName + "Factory";
                sb.AppendLine($"| {sourceName} | {factoryInterface} | {factoryImpl} | (host) |");
            }

            // Plugin factories
            foreach (var (assembly, type) in pluginFactories.OrderBy(x => x.Type.ShortName))
            {
                var factoryInterface = "I" + type.ShortName + "Factory";
                var factoryImpl = type.ShortName + "Factory";
                sb.AppendLine($"| {type.ShortName} | {factoryInterface} | {factoryImpl} | {assembly} |");
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

        // Keyed Services section
        var keyedTypes = types.Where(t => t.ServiceKeys.Length > 0).ToList();
        if (keyedTypes.Any())
        {
            sb.AppendLine($"## Keyed Services ({keyedTypes.Sum(t => t.ServiceKeys.Length)})");
            sb.AppendLine();
            sb.AppendLine("| Key | Interface | Implementation | Lifetime |");
            sb.AppendLine("|-----|-----------|----------------|----------|");

            foreach (var type in keyedTypes.OrderBy(t => t.TypeName))
            {
                foreach (var key in type.ServiceKeys)
                {
                    var iface = type.InterfaceNames.FirstOrDefault() ?? "-";
                    sb.AppendLine($"| `\"{key}\"` | {GetShortTypeName(iface)} | {GetShortTypeName(type.TypeName)} | {type.Lifetime} |");
                }
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

    private static IReadOnlyList<DiscoveredFactory> FilterFactories(IReadOnlyList<DiscoveredFactory> factories, HashSet<string> filter)
    {
        if (filter == null || filter.Count == 0)
            return factories;

        return factories.Where(f => 
            filter.Contains(f.TypeName) ||
            filter.Contains(GetShortTypeName(f.TypeName)) ||
            filter.Contains(StripGlobalPrefix(f.TypeName)))
                        .ToList();
    }

    /// <summary>
    /// Filters out nested options types.
    /// A nested options type is one that is used as a property type in another options type.
    /// These should not be registered separately - they are bound as part of their parent.
    /// </summary>
    private static List<DiscoveredOptions> FilterNestedOptions(List<DiscoveredOptions> options, Compilation compilation)
    {
        // Build a set of all options type names
        var optionsTypeNames = new HashSet<string>(options.Select(o => o.TypeName));

        // Find all options types that are used as properties in other options types
        var nestedTypeNames = new HashSet<string>();

        foreach (var opt in options)
        {
            // Find the type symbol for this options type
            var typeSymbol = FindTypeSymbol(compilation, opt.TypeName);
            if (typeSymbol == null)
                continue;

            // Check all properties of this type
            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is not IPropertySymbol property)
                    continue;

                // Skip non-class property types (primitives, structs, etc.)
                if (property.Type is not INamedTypeSymbol propertyType)
                    continue;

                if (propertyType.TypeKind != TypeKind.Class)
                    continue;

                // Get the fully qualified name of the property type
                var propertyTypeName = TypeDiscoveryHelper.GetFullyQualifiedName(propertyType);

                // If this property type is also an [Options] type, mark it as nested
                if (optionsTypeNames.Contains(propertyTypeName))
                {
                    nestedTypeNames.Add(propertyTypeName);
                }
            }
        }

        // Return only root options (those not used as properties in other options)
        return options.Where(o => !nestedTypeNames.Contains(o.TypeName)).ToList();
    }

    /// <summary>
    /// Finds a type symbol by its fully qualified name.
    /// </summary>
    private static INamedTypeSymbol? FindTypeSymbol(Compilation compilation, string fullyQualifiedName)
    {
        // Strip global:: prefix if present
        var typeName = fullyQualifiedName.StartsWith("global::")
            ? fullyQualifiedName.Substring(8)
            : fullyQualifiedName;

        return compilation.GetTypeByMetadataName(typeName);
    }

    /// <summary>
    /// Expands open generic decorators into concrete decorator registrations
    /// for each discovered closed implementation of the open generic interface.
    /// </summary>
    private static void ExpandOpenDecorators(
        IReadOnlyList<DiscoveredType> injectableTypes,
        IReadOnlyList<DiscoveredOpenDecorator> openDecorators,
        List<DiscoveredDecorator> decorators)
    {
        // Group injectable types by the open generic interfaces they implement
        var interfaceImplementations = new Dictionary<INamedTypeSymbol, List<(INamedTypeSymbol closedInterface, DiscoveredType type)>>(SymbolEqualityComparer.Default);

        foreach (var discoveredType in injectableTypes)
        {
            // We need to check each interface this type implements to see if it's a closed version of an open generic
            foreach (var openDecorator in openDecorators)
            {
                // Check if this type implements the open generic interface
                foreach (var interfaceName in discoveredType.InterfaceNames)
                {
                    // This is string-based matching - we need to match the interface name pattern
                    // For example, if open generic is IHandler<> and the interface is IHandler<Order>, we should match
                    var openGenericName = TypeDiscoveryHelper.GetFullyQualifiedName(openDecorator.OpenGenericInterface);
                    
                    // Extract the base name (before the <>)
                    var openGenericBaseName = GetGenericBaseName(openGenericName);
                    var interfaceBaseName = GetGenericBaseName(interfaceName);
                    
                    if (openGenericBaseName == interfaceBaseName)
                    {
                        // This interface is a closed version of the open generic
                        // Create a closed decorator registration
                        var closedDecoratorTypeName = CreateClosedGenericType(
                            TypeDiscoveryHelper.GetFullyQualifiedName(openDecorator.DecoratorType),
                            interfaceName,
                            openGenericName);

                        decorators.Add(new DiscoveredDecorator(
                            closedDecoratorTypeName,
                            interfaceName,
                            openDecorator.Order,
                            openDecorator.AssemblyName,
                            openDecorator.SourceFilePath));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Extracts the base name from a generic type (e.g., "global::Namespace.IHandler{T}" becomes "global::Namespace.IHandler").
    /// </summary>
    private static string GetGenericBaseName(string typeName)
    {
        var angleBracketIndex = typeName.IndexOf('<');
        return angleBracketIndex >= 0 ? typeName.Substring(0, angleBracketIndex) : typeName;
    }

    /// <summary>
    /// Creates a closed generic type name from an open generic decorator and a closed interface.
    /// For example: LoggingDecorator{T} + IHandler{Order} = LoggingDecorator{Order}
    /// </summary>
    private static string CreateClosedGenericType(string openDecoratorTypeName, string closedInterfaceName, string openInterfaceName)
    {
        // Extract the type arguments from the closed interface
        var closedArgs = ExtractGenericArguments(closedInterfaceName);
        
        // Replace the type parameters in the open decorator with the closed arguments
        var openDecoratorBaseName = GetGenericBaseName(openDecoratorTypeName);
        
        if (closedArgs.Length == 0)
            return openDecoratorTypeName;
        
        return $"{openDecoratorBaseName}<{string.Join(", ", closedArgs)}>";
    }

    /// <summary>
    /// Extracts the generic type arguments from a closed generic type name.
    /// For example: "IHandler{Order, Payment}" returns ["Order", "Payment"]
    /// </summary>
    private static string[] ExtractGenericArguments(string typeName)
    {
        var angleBracketIndex = typeName.IndexOf('<');
        if (angleBracketIndex < 0)
            return Array.Empty<string>();

        var argsStart = angleBracketIndex + 1;
        var argsEnd = typeName.LastIndexOf('>');
        if (argsEnd <= argsStart)
            return Array.Empty<string>();

        var argsString = typeName.Substring(argsStart, argsEnd - argsStart);
        
        // Handle nested generics by parsing with bracket depth tracking
        var args = new List<string>();
        var depth = 0;
        var start = 0;
        
        for (int i = 0; i < argsString.Length; i++)
        {
            var c = argsString[i];
            if (c == '<') depth++;
            else if (c == '>') depth--;
            else if (c == ',' && depth == 0)
            {
                args.Add(argsString.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        
        // Add the last argument
        if (start < argsString.Length)
            args.Add(argsString.Substring(start).Trim());
        
        return args.ToArray();
    }

    private static string StripGlobalPrefix(string name)
    {
        return name.StartsWith("global::", StringComparison.Ordinal) 
            ? name.Substring(8) 
            : name;
    }

    private static int CalculateMaxDependencyDepth(IReadOnlyList<DiscoveredType> types)
    {
        if (types.Count == 0) return 0;

        // Build a lookup from interface/type name to the type that provides it
        var providerLookup = new Dictionary<string, DiscoveredType>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            providerLookup[GetShortTypeName(type.TypeName)] = type;
            foreach (var iface in type.InterfaceNames)
                providerLookup[GetShortTypeName(iface)] = type;
        }

        // Calculate depth for each type using memoization
        var depthCache = new Dictionary<string, int>(StringComparer.Ordinal);
        int maxDepth = 0;

        foreach (var type in types)
        {
            var depth = GetDepth(type, providerLookup, depthCache, new HashSet<string>());
            if (depth > maxDepth) maxDepth = depth;
        }

        return maxDepth;
    }

    private static int GetDepth(DiscoveredType type, Dictionary<string, DiscoveredType> providerLookup, Dictionary<string, int> cache, HashSet<string> visiting)
    {
        var key = GetShortTypeName(type.TypeName);
        
        if (cache.TryGetValue(key, out var cached))
            return cached;

        // Cycle detection
        if (!visiting.Add(key))
            return 0;

        int maxChildDepth = 0;
        foreach (var dep in type.ConstructorParameterTypes)
        {
            var depShort = GetShortTypeName(dep);
            if (providerLookup.TryGetValue(depShort, out var depType))
            {
                var childDepth = GetDepth(depType, providerLookup, cache, visiting);
                if (childDepth > maxChildDepth)
                    maxChildDepth = childDepth;
            }
        }

        visiting.Remove(key);
        var result = maxChildDepth + 1;
        cache[key] = result;
        return result;
    }

    private static List<(string TypeName, int DependentCount)> CalculateHubServices(IReadOnlyList<DiscoveredType> types, int minDependents)
    {
        // Count how many times each type/interface appears as a dependency
        var dependentCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var type in types)
        {
            foreach (var dep in type.ConstructorParameterTypes)
            {
                var depShort = GetShortTypeName(dep);
                if (!dependentCounts.ContainsKey(depShort))
                    dependentCounts[depShort] = 0;
                dependentCounts[depShort]++;
            }
        }

        // Find types that are depended upon by minDependents or more services
        var hubs = new List<(string TypeName, int DependentCount)>();
        foreach (var type in types)
        {
            var shortName = GetShortTypeName(type.TypeName);
            var count = 0;

            // Check if this type's name or any of its interfaces is depended upon
            if (dependentCounts.TryGetValue(shortName, out var c1))
                count += c1;

            foreach (var iface in type.InterfaceNames)
            {
                if (dependentCounts.TryGetValue(GetShortTypeName(iface), out var c2))
                    count += c2;
            }

            if (count >= minDependents)
                hubs.Add((type.TypeName, count));
        }

        return hubs.OrderByDescending(h => h.DependentCount).ToList();
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

    /// <summary>
    /// Gets the fully qualified validator class name for an options type.
    /// E.g., "global::TestApp.StripeOptions" -> "global::TestApp.Generated.StripeOptionsValidator"
    /// </summary>
    private static string GetValidatorClassName(string optionsTypeName)
    {
        var shortName = GetShortTypeName(optionsTypeName);
        
        // Get namespace from fully qualified name
        var name = optionsTypeName;
        if (name.StartsWith("global::", StringComparison.Ordinal))
            name = name.Substring(8);
        
        var lastDot = name.LastIndexOf('.');
        var ns = lastDot >= 0 ? name.Substring(0, lastDot) : "";
        
        var validatorName = shortName + "Validator";
        return string.IsNullOrEmpty(ns)
            ? $"global::{validatorName}"
            : $"global::{ns}.Generated.{validatorName}";
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

    /// <summary>
    /// Discovers types from referenced assemblies with [GenerateTypeRegistry] for diagnostics purposes.
    /// Unlike the main discovery, this includes internal types since we're just showing them in diagnostics.
    /// </summary>
    private static Dictionary<string, List<DiagnosticTypeInfo>> DiscoverReferencedAssemblyTypesForDiagnostics(Compilation compilation)
    {
        var result = new Dictionary<string, List<DiagnosticTypeInfo>>();
        
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                // Skip the current assembly
                if (SymbolEqualityComparer.Default.Equals(assemblySymbol, compilation.Assembly))
                    continue;
                    
                if (!TypeDiscoveryHelper.HasGenerateTypeRegistryAttribute(assemblySymbol))
                    continue;

                var assemblyTypes = new List<DiagnosticTypeInfo>();
                
                // First pass: collect intercepted service names so we can identify their proxies
                var interceptedServiceNames = new HashSet<string>();
                foreach (var typeSymbol in TypeDiscoveryHelper.GetAllTypes(assemblySymbol.GlobalNamespace))
                {
                    if (TypeDiscoveryHelper.HasInterceptAttributes(typeSymbol))
                    {
                        interceptedServiceNames.Add(typeSymbol.Name);
                    }
                }
                
                foreach (var typeSymbol in TypeDiscoveryHelper.GetAllTypes(assemblySymbol.GlobalNamespace))
                {
                    // Check if it's a registerable type (injectable, plugin, factory source, or interceptor)
                    var hasFactoryAttr = TypeDiscoveryHelper.HasGenerateFactoryAttribute(typeSymbol);
                    var hasInterceptAttr = TypeDiscoveryHelper.HasInterceptAttributes(typeSymbol);
                    var isInterceptorProxy = typeSymbol.Name.EndsWith("_InterceptorProxy");
                    
                    if (!hasFactoryAttr && !hasInterceptAttr && !isInterceptorProxy &&
                        !TypeDiscoveryHelper.WouldBeInjectableIgnoringAccessibility(typeSymbol) &&
                        !TypeDiscoveryHelper.WouldBePluginIgnoringAccessibility(typeSymbol))
                        continue;

                    var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                    var shortName = typeSymbol.Name;
                    var lifetime = TypeDiscoveryHelper.DetermineLifetime(typeSymbol) ?? GeneratorLifetime.Singleton;
                    var interfaces = TypeDiscoveryHelper.GetRegisterableInterfaces(typeSymbol)
                        .Select(i => TypeDiscoveryHelper.GetFullyQualifiedName(i))
                        .ToArray();
                    var dependencies = TypeDiscoveryHelper.GetBestConstructorParameters(typeSymbol)?
                        .ToArray() ?? Array.Empty<string>();
                    var isDecorator = TypeDiscoveryHelper.HasDecoratorForAttribute(typeSymbol) || 
                                      TypeDiscoveryHelper.HasOpenDecoratorForAttribute(typeSymbol);
                    var isPlugin = TypeDiscoveryHelper.WouldBePluginIgnoringAccessibility(typeSymbol);
                    var keyedValues = TypeDiscoveryHelper.GetKeyedServiceKeys(typeSymbol);
                    var keyedValue = keyedValues.Length > 0 ? keyedValues[0] : null;
                    
                    // Check if this service has an interceptor proxy (its name + "_InterceptorProxy" exists)
                    var hasInterceptorProxy = interceptedServiceNames.Contains(shortName);

                    assemblyTypes.Add(new DiagnosticTypeInfo(
                        typeName,
                        shortName,
                        lifetime,
                        interfaces,
                        dependencies,
                        isDecorator,
                        isPlugin,
                        hasFactoryAttr,
                        keyedValue,
                        isInterceptor: hasInterceptAttr,
                        hasInterceptorProxy: hasInterceptorProxy));
                }

                if (assemblyTypes.Count > 0)
                {
                    result[assemblySymbol.Name] = assemblyTypes;
                }
            }
        }
        
        return result;
    }
}
