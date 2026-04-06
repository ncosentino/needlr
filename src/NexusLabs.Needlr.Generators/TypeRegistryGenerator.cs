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
            
            // Check if this is an AOT project
            var isAotProject = IsAotProject(configOptions);

            var discoveryResult = DiscoverTypes(
                compilation,
                info.NamespacePrefixes,
                info.IncludeSelf);

            // Discover referenced assemblies with [GenerateTypeRegistry] for forced loading.
            // Done early so the empty-result check below can include this in its decision.
            // Note: Order of force-loading doesn't matter; ordering is applied at service registration time
            var referencedAssemblies = AssemblyDiscoveryHelper.DiscoverReferencedAssembliesWithTypeRegistry(compilation)
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // If nothing was discovered — no injectable types, factories, providers, options,
            // interceptors, hosted services, plugins, no referenced assemblies to force-load,
            // no inaccessible type errors, and no missing TypeRegistry warnings —
            // emit nothing. This avoids compile errors in projects that don't reference Needlr
            // injection packages (e.g. a documentation-only project): the generated bootstrap code
            // references types from those packages and would fail to build without them.
            if (discoveryResult.InjectableTypes.Count == 0 &&
                discoveryResult.PluginTypes.Count == 0 &&
                discoveryResult.Decorators.Count == 0 &&
                discoveryResult.InterceptedServices.Count == 0 &&
                discoveryResult.Factories.Count == 0 &&
                discoveryResult.Options.Count == 0 &&
                discoveryResult.HttpClients.Count == 0 &&
                discoveryResult.HostedServices.Count == 0 &&
                discoveryResult.Providers.Count == 0 &&
                discoveryResult.InaccessibleTypes.Count == 0 &&
                discoveryResult.MissingTypeRegistryPlugins.Count == 0 &&
                referencedAssemblies.Count == 0)
            {
                return;
            }

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
            
            // NDLRGEN020: Previously reported error if [Options] used in AOT project
            // Now removed for parity - we generate best-effort code and let unsupported 
            // types fail at runtime (matching non-AOT ConfigurationBinder behavior)

            // NDLRGEN021: Report warning for non-partial positional records
            foreach (var opt in discoveryResult.Options.Where(o => o.IsNonPartialPositionalRecord))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.PositionalRecordMustBePartial,
                    Location.None,
                    opt.TypeName));
            }

            // NDLRGEN022: Detect disposable captive dependencies using inferred lifetimes
            CaptiveDependencyAnalyzer.ReportDisposableCaptiveDependencies(spc, discoveryResult);

            var sourceText = GenerateTypeRegistrySource(discoveryResult, assemblyName, breadcrumbs, projectDirectory, isAotProject);
            spc.AddSource("TypeRegistry.g.cs", SourceText.From(sourceText, Encoding.UTF8));

            var bootstrapText = CodeGen.BootstrapCodeGenerator.GenerateModuleInitializerBootstrapSource(assemblyName, referencedAssemblies, breadcrumbs, discoveryResult.Factories.Count > 0, discoveryResult.Options.Count > 0 || discoveryResult.HttpClients.Count > 0, discoveryResult.Providers.Count > 0);
            spc.AddSource("NeedlrSourceGenBootstrap.g.cs", SourceText.From(bootstrapText, Encoding.UTF8));

            // Generate interceptor proxy classes if any were discovered
            if (discoveryResult.InterceptedServices.Count > 0)
            {
                var interceptorProxiesText = CodeGen.InterceptorCodeGenerator.GenerateInterceptorProxiesSource(discoveryResult.InterceptedServices, assemblyName, breadcrumbs, projectDirectory);
                spc.AddSource("InterceptorProxies.g.cs", SourceText.From(interceptorProxiesText, Encoding.UTF8));
            }

            // Generate factory classes if any were discovered
            if (discoveryResult.Factories.Count > 0)
            {
                var factoriesText = CodeGen.FactoryCodeGenerator.GenerateFactoriesSource(discoveryResult.Factories, assemblyName, breadcrumbs, projectDirectory);
                spc.AddSource("Factories.g.cs", SourceText.From(factoriesText, Encoding.UTF8));
            }

            // Generate provider classes if any were discovered
            if (discoveryResult.Providers.Count > 0)
            {
                // Interface-based providers go in the Generated namespace
                var interfaceProviders = discoveryResult.Providers.Where(p => p.IsInterface).ToList();
                if (interfaceProviders.Count > 0)
                {
                    var providersText = CodeGen.ProviderCodeGenerator.GenerateProvidersSource(interfaceProviders, assemblyName, breadcrumbs, projectDirectory);
                    spc.AddSource("Providers.g.cs", SourceText.From(providersText, Encoding.UTF8));
                }

                // Shorthand class providers need to be generated in their original namespace
                var classProviders = discoveryResult.Providers.Where(p => !p.IsInterface && p.IsPartial).ToList();
                foreach (var provider in classProviders)
                {
                    var providerText = CodeGen.ProviderCodeGenerator.GenerateShorthandProviderSource(provider, assemblyName, breadcrumbs, projectDirectory);
                    spc.AddSource($"Provider.{provider.SimpleTypeName}.g.cs", SourceText.From(providerText, Encoding.UTF8));
                }
            }

            // Generate options validator classes if any have validation methods
            var optionsWithValidators = discoveryResult.Options.Where(o => o.HasValidatorMethod).ToList();
            if (optionsWithValidators.Count > 0)
            {
                var validatorsText = CodeGen.OptionsCodeGenerator.GenerateOptionsValidatorsSource(optionsWithValidators, assemblyName, breadcrumbs, projectDirectory);
                spc.AddSource("OptionsValidators.g.cs", SourceText.From(validatorsText, Encoding.UTF8));
            }

            // Generate DataAnnotations validator classes if any have DataAnnotation attributes
            var optionsWithDataAnnotations = discoveryResult.Options.Where(o => o.HasDataAnnotations).ToList();
            if (optionsWithDataAnnotations.Count > 0)
            {
                var dataAnnotationsValidatorsText = CodeGen.OptionsCodeGenerator.GenerateDataAnnotationsValidatorsSource(optionsWithDataAnnotations, assemblyName, breadcrumbs, projectDirectory);
                spc.AddSource("OptionsDataAnnotationsValidators.g.cs", SourceText.From(dataAnnotationsValidatorsText, Encoding.UTF8));
            }

            // Generate parameterless constructors for partial positional records with [Options]
            var optionsNeedingConstructors = discoveryResult.Options.Where(o => o.NeedsGeneratedConstructor).ToList();
            if (optionsNeedingConstructors.Count > 0)
            {
                var constructorsText = CodeGen.OptionsCodeGenerator.GeneratePositionalRecordConstructorsSource(optionsNeedingConstructors, assemblyName, breadcrumbs, projectDirectory);
                spc.AddSource("OptionsConstructors.g.cs", SourceText.From(constructorsText, Encoding.UTF8));
            }

            // Generate ServiceCatalog for runtime introspection
            var catalogText = CodeGen.ServiceCatalogCodeGenerator.GenerateServiceCatalogSource(discoveryResult, assemblyName, projectDirectory, breadcrumbs);
            spc.AddSource("ServiceCatalog.g.cs", SourceText.From(catalogText, Encoding.UTF8));

            // Generate diagnostic output files if configured
            var diagnosticOptions = GetDiagnosticOptions(configOptions);
            if (diagnosticOptions.Enabled)
            {
                var referencedAssemblyTypes = AssemblyDiscoveryHelper.DiscoverReferencedAssemblyTypesForDiagnostics(compilation);
                var diagnosticsText = DiagnosticsGenerator.GenerateDiagnosticsSource(discoveryResult, assemblyName, projectDirectory, diagnosticOptions, referencedAssemblies, referencedAssemblyTypes);
                spc.AddSource("NeedlrDiagnostics.g.cs", SourceText.From(diagnosticsText, Encoding.UTF8));
            }

            // Generate IDE graph export if configured
            if (ShouldExportGraph(configOptions))
            {
                // Discover types from referenced assemblies with [GenerateTypeRegistry] for graph inclusion
                var referencedAssemblyTypesForGraph = AssemblyDiscoveryHelper.DiscoverReferencedAssemblyTypesForGraph(compilation);

                var graphJson = Export.GraphExporter.GenerateGraphJson(
                    discoveryResult,
                    assemblyName,
                    projectDirectory,
                    diagnostics: null,
                    referencedAssemblyTypes: referencedAssemblyTypesForGraph);
                
                // Embed graph as a comment in a generated file so it's accessible
                // The actual JSON is written to obj folder via the generated code
                var graphSourceText = Export.GraphExporter.GenerateGraphExportSource(graphJson, assemblyName, breadcrumbs, projectDirectory);
                spc.AddSource("NeedlrGraph.g.cs", SourceText.From(graphSourceText, Encoding.UTF8));
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

    /// <summary>
    /// Checks if the IDE graph export is enabled.
    /// </summary>
    private static bool ShouldExportGraph(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider configOptions)
    {
        // Export graph is disabled by default
        // Enable with NeedlrExportGraph=true in project file
        if (configOptions.GlobalOptions.TryGetValue("build_property.NeedlrExportGraph", out var exportGraph) &&
            exportGraph.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if the project is configured for AOT compilation.
    /// Returns true if either PublishAot or IsAotCompatible is set to true.
    /// </summary>
    private static bool IsAotProject(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider configOptions)
    {
        if (configOptions.GlobalOptions.TryGetValue("build_property.PublishAot", out var publishAot) &&
            publishAot.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        if (configOptions.GlobalOptions.TryGetValue("build_property.IsAotCompatible", out var isAotCompatible) &&
            isAotCompatible.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        return false;
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
        var decorators = new List<DiscoveredDecorator>();
        var openDecorators = new List<DiscoveredOpenDecorator>();
        var interceptedServices = new List<DiscoveredInterceptedService>();
        var factories = new List<DiscoveredFactory>();
        var options = new List<DiscoveredOptions>();
        var hostedServices = new List<DiscoveredHostedService>();
        var providers = new List<DiscoveredProvider>();
        var httpClients = new List<DiscoveredHttpClient>();
        var inaccessibleTypes = new List<InaccessibleType>();
        var prefixList = namespacePrefixes?.ToList();
        
        // Compute the generated namespace for the current assembly
        var currentAssemblyName = compilation.Assembly.Name;
        var safeAssemblyName = GeneratorHelpers.SanitizeIdentifier(currentAssemblyName);
        var generatedNamespace = $"{safeAssemblyName}.Generated";

        // Collect types from the current compilation if includeSelf is true
        if (includeSelf)
        {
            CollectTypesFromAssembly(compilation.Assembly, prefixList, injectableTypes, pluginTypes, decorators, openDecorators, interceptedServices, factories, options, hostedServices, providers, httpClients, inaccessibleTypes, compilation, isCurrentAssembly: true, generatedNamespace);
        }

        // Collect types from all referenced assemblies
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                // Skip assemblies that already have [GenerateTypeRegistry] — those assemblies
                // register their own types at runtime via their own TypeRegistry and cascade
                // loading. Scanning them here would trigger false NDLRGEN001 errors for their
                // internal types.
                if (TypeDiscoveryHelper.HasGenerateTypeRegistryAttribute(assemblySymbol))
                    continue;

                // For referenced assemblies, they use their own generated namespace
                var refSafeAssemblyName = GeneratorHelpers.SanitizeIdentifier(assemblySymbol.Name);
                var refGeneratedNamespace = $"{refSafeAssemblyName}.Generated";
                CollectTypesFromAssembly(assemblySymbol, prefixList, injectableTypes, pluginTypes, decorators, openDecorators, interceptedServices, factories, options, hostedServices, providers, httpClients, inaccessibleTypes, compilation, isCurrentAssembly: false, refGeneratedNamespace);
            }
        }

        // Expand open generic decorators into closed decorator registrations
        if (openDecorators.Count > 0)
        {
            CodeGen.DecoratorsCodeGenerator.ExpandOpenDecorators(injectableTypes, openDecorators, decorators);
        }

        // Filter out nested options types (types used as properties in other options types)
        if (options.Count > 1)
        {
            options = OptionsDiscoveryHelper.FilterNestedOptions(options, compilation);
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

        return new DiscoveryResult(injectableTypes, pluginTypes, decorators, inaccessibleTypes, missingTypeRegistryPlugins, interceptedServices, factories, options, hostedServices, providers, httpClients);
    }

    private static void CollectTypesFromAssembly(
        IAssemblySymbol assembly,
        IReadOnlyList<string>? namespacePrefixes,
        List<DiscoveredType> injectableTypes,
        List<DiscoveredPlugin> pluginTypes,
        List<DiscoveredDecorator> decorators,
        List<DiscoveredOpenDecorator> openDecorators,
        List<DiscoveredInterceptedService> interceptedServices,
        List<DiscoveredFactory> factories,
        List<DiscoveredOptions> options,
        List<DiscoveredHostedService> hostedServices,
        List<DiscoveredProvider> providers,
        List<DiscoveredHttpClient> httpClients,
        List<InaccessibleType> inaccessibleTypes,
        Compilation compilation,
        bool isCurrentAssembly,
        string generatedNamespace)
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
            if (OptionsAttributeHelper.HasOptionsAttribute(typeSymbol))
            {
                var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                var optionsAttrs = OptionsAttributeHelper.GetOptionsAttributes(typeSymbol);
                var sourceFilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;

                // Detect positional record (record with primary constructor parameters)
                var positionalRecordInfo = OptionsDiscoveryHelper.DetectPositionalRecord(typeSymbol);

                // Extract bindable properties for AOT code generation
                var properties = OptionsDiscoveryHelper.ExtractBindableProperties(typeSymbol);

                foreach (var optionsAttr in optionsAttrs)
                {
                    // Determine validator type and method
                    var validatorTypeSymbol = optionsAttr.ValidatorType;
                    var targetType = validatorTypeSymbol ?? typeSymbol; // Look for method on options class or external validator
                    var methodName = optionsAttr.ValidateMethod ?? "Validate"; // Convention: "Validate"

                    // Find validation method using convention-based discovery
                    var validatorMethodInfo = OptionsAttributeHelper.FindValidationMethod(targetType, methodName);
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
                        validatorTypeName,
                        positionalRecordInfo,
                        properties));
                }
            }

            // Check for [HttpClientOptions] attribute
            if (HttpClientOptionsAttributeHelper.HasHttpClientOptionsAttribute(typeSymbol))
            {
                var httpAttrInfo = HttpClientOptionsAttributeHelper.GetHttpClientOptionsAttribute(typeSymbol);
                if (httpAttrInfo.HasValue)
                {
                    // Try to read a literal ClientName property body, if any.
                    var clientNamePropResult = HttpClientOptionsAttributeHelper.TryGetClientNameProperty(typeSymbol, out var literalValue);
                    var propertyNameFromType = clientNamePropResult == ClientNamePropertyResult.Literal ? literalValue : null;

                    if (HttpClientOptionsAttributeHelper.TryResolveClientName(
                        typeSymbol,
                        httpAttrInfo.Value,
                        propertyNameFromType,
                        out var resolvedClientName))
                    {
                        var httpSectionName = HttpClientOptionsAttributeHelper.ResolveSectionName(httpAttrInfo.Value, resolvedClientName);
                        var httpTypeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                        var httpSourceFilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;
                        var capabilities = HttpClientOptionsAttributeHelper.DetectCapabilities(typeSymbol);

                        httpClients.Add(new DiscoveredHttpClient(
                            httpTypeName,
                            resolvedClientName,
                            httpSectionName,
                            assembly.Name,
                            capabilities,
                            httpSourceFilePath));
                    }
                }
            }

            // Check for [GenerateFactory] attribute - these types get factories instead of direct registration
            if (FactoryDiscoveryHelper.HasGenerateFactoryAttribute(typeSymbol))
            {
                var factoryConstructors = FactoryDiscoveryHelper.GetFactoryConstructors(typeSymbol);
                if (factoryConstructors.Count > 0)
                {
                    // Has at least one constructor with runtime params - generate factory
                    var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                    var interfaces = TypeDiscoveryHelper.GetRegisterableInterfaces(typeSymbol);
                    var interfaceNames = interfaces.Select(i => TypeDiscoveryHelper.GetFullyQualifiedName(i)).ToArray();
                    var generationMode = FactoryDiscoveryHelper.GetFactoryGenerationMode(typeSymbol);
                    var returnTypeOverride = FactoryDiscoveryHelper.GetFactoryReturnInterfaceType(typeSymbol);
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
            var openDecoratorInfos = OpenDecoratorDiscoveryHelper.GetOpenDecoratorForAttributes(typeSymbol);
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
            if (InterceptorDiscoveryHelper.HasInterceptAttributes(typeSymbol))
            {
                var lifetime = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);
                if (lifetime.HasValue)
                {
                    var classLevelInterceptors = InterceptorDiscoveryHelper.GetInterceptAttributes(typeSymbol);
                    var methodLevelInterceptors = InterceptorDiscoveryHelper.GetMethodLevelInterceptAttributes(typeSymbol);
                    var methods = InterceptorDiscoveryHelper.GetInterceptedMethods(typeSymbol, classLevelInterceptors, methodLevelInterceptors);

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

            // Check for injectable types (but skip types that are providers, which are handled separately)
            if (TypeDiscoveryHelper.IsInjectableType(typeSymbol, isCurrentAssembly) && !ProviderDiscoveryHelper.HasProviderAttribute(typeSymbol))
            {
                // Determine lifetime first - only include types that are actually injectable
                var lifetime = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);
                if (lifetime.HasValue)
                {
                    var interfaces = TypeDiscoveryHelper.GetRegisterableInterfaces(typeSymbol);
                    var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                    var interfaceNames = interfaces.Select(i => TypeDiscoveryHelper.GetFullyQualifiedName(i)).ToArray();
                    
                    // Capture interface locations for navigation
                    var interfaceInfos = interfaces.Select(i =>
                    {
                        var ifaceLocation = i.Locations.FirstOrDefault();
                        var ifaceFilePath = ifaceLocation?.SourceTree?.FilePath;
                        var ifaceLine = ifaceLocation?.GetLineSpan().StartLinePosition.Line + 1 ?? 0;
                        return new InterfaceInfo(TypeDiscoveryHelper.GetFullyQualifiedName(i), ifaceFilePath, ifaceLine);
                    }).ToArray();
                    
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
                    
                    // Get source file path and line for breadcrumbs (null for external assemblies)
                    var location = typeSymbol.Locations.FirstOrDefault();
                    var sourceFilePath = location?.SourceTree?.FilePath;
                    var sourceLine = location?.GetLineSpan().StartLinePosition.Line + 1 ?? 0; // Convert to 1-based

                    // Get [Keyed] attribute keys
                    var serviceKeys = TypeDiscoveryHelper.GetKeyedServiceKeys(typeSymbol);

                    // Check if this type implements IDisposable or IAsyncDisposable
                    var isDisposable = TypeDiscoveryHelper.IsDisposableType(typeSymbol);

                    injectableTypes.Add(new DiscoveredType(typeName, interfaceNames, assembly.Name, lifetime.Value, constructorParams, serviceKeys, sourceFilePath, sourceLine, isDisposable, interfaceInfos));
                }
            }

            // Check for hosted service types (BackgroundService or IHostedService implementations)
            if (TypeDiscoveryHelper.IsHostedServiceType(typeSymbol, isCurrentAssembly))
            {
                var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                var constructorParams = TypeDiscoveryHelper.GetBestConstructorParametersWithKeys(typeSymbol)?.ToArray() ?? [];
                var sourceFilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;
                
                hostedServices.Add(new DiscoveredHostedService(
                    typeName,
                    assembly.Name,
                    GeneratorLifetime.Singleton, // Hosted services are always singleton
                    constructorParams,
                    sourceFilePath));
            }

            // Check for [Provider] attribute
            if (ProviderDiscoveryHelper.HasProviderAttribute(typeSymbol))
            {
                var discoveredProvider = ProviderDiscoveryHelper.DiscoverProvider(typeSymbol, assembly.Name, generatedNamespace);
                if (discoveredProvider.HasValue)
                {
                    providers.Add(discoveredProvider.Value);
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
                    var order = PluginOrderHelper.GetPluginOrder(typeSymbol);

                    pluginTypes.Add(new DiscoveredPlugin(typeName, interfaceNames, assembly.Name, attributeNames, sourceFilePath, order));
                }
            }

            // Check for IHubRegistrationPlugin implementations
            // NOTE: SignalR hub discovery is now handled by NexusLabs.Needlr.SignalR.Generators

            // Check for SemanticKernel plugin types (classes/statics with [KernelFunction] methods)
            // NOTE: SemanticKernel plugin discovery is now handled by NexusLabs.Needlr.SemanticKernel.Generators
        }
    }

    private static string GenerateTypeRegistrySource(DiscoveryResult discoveryResult, string assemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory, bool isAotProject)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = GeneratorHelpers.SanitizeIdentifier(assemblyName);
        var hasOptions = discoveryResult.Options.Count > 0;
        var hasHttpClients = discoveryResult.HttpClients.Count > 0;
        var hasConfigBoundRegistrations = hasOptions || hasHttpClients;

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Type Registry");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        if (hasConfigBoundRegistrations)
        {
            builder.AppendLine("using Microsoft.Extensions.Configuration;");
            if (isAotProject || hasHttpClients)
            {
                builder.AppendLine("using Microsoft.Extensions.Options;");
            }
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

        CodeGen.InjectableTypesCodeGenerator.GenerateInjectableTypesArray(builder, discoveryResult.InjectableTypes, breadcrumbs, projectDirectory);
        builder.AppendLine();
        CodeGen.PluginsCodeGenerator.GeneratePluginTypesArray(builder, discoveryResult.PluginTypes, breadcrumbs, projectDirectory);

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

        if (hasConfigBoundRegistrations)
        {
            builder.AppendLine();
            GenerateRegisterOptionsMethod(builder, discoveryResult.Options, discoveryResult.HttpClients, safeAssemblyName, breadcrumbs, projectDirectory, isAotProject);
        }

        if (discoveryResult.Providers.Count > 0)
        {
            builder.AppendLine();
            CodeGen.DecoratorsCodeGenerator.GenerateRegisterProvidersMethod(builder, discoveryResult.Providers, safeAssemblyName, breadcrumbs, projectDirectory);
        }

        builder.AppendLine();
        CodeGen.DecoratorsCodeGenerator.GenerateApplyDecoratorsMethod(builder, discoveryResult.Decorators, discoveryResult.InterceptedServices.Count > 0, discoveryResult.HostedServices.Count > 0, safeAssemblyName, breadcrumbs, projectDirectory);

        if (discoveryResult.HostedServices.Count > 0)
        {
            builder.AppendLine();
            CodeGen.DecoratorsCodeGenerator.GenerateRegisterHostedServicesMethod(builder, discoveryResult.HostedServices, breadcrumbs, projectDirectory);
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void GenerateRegisterOptionsMethod(StringBuilder builder, IReadOnlyList<DiscoveredOptions> options, IReadOnlyList<DiscoveredHttpClient> httpClients, string safeAssemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory, bool isAotProject)
    {
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Registers all discovered options types with the service collection.");
        builder.AppendLine("    /// This binds configuration sections to strongly-typed options classes,");
        builder.AppendLine("    /// and wires up named HttpClient registrations for [HttpClientOptions] types.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <param name=\"services\">The service collection to configure.</param>");
        builder.AppendLine("    /// <param name=\"configuration\">The configuration root to bind options from.</param>");
        builder.AppendLine("    public static void RegisterOptions(IServiceCollection services, IConfiguration configuration)");
        builder.AppendLine("    {");

        if (options.Count == 0 && httpClients.Count == 0)
        {
            breadcrumbs.WriteInlineComment(builder, "        ", "No options or HttpClient types discovered");
        }
        else
        {
            if (options.Count > 0)
            {
                if (isAotProject)
                {
                    CodeGen.OptionsCodeGenerator.GenerateAotOptionsRegistration(builder, options, safeAssemblyName, breadcrumbs, projectDirectory);
                }
                else
                {
                    CodeGen.OptionsCodeGenerator.GenerateReflectionOptionsRegistration(builder, options, safeAssemblyName, breadcrumbs);
                }
            }

            if (httpClients.Count > 0)
            {
                CodeGen.HttpClientCodeGenerator.EmitHttpClientRegistrations(builder, httpClients);
            }
        }

        builder.AppendLine("    }");
    }

}
