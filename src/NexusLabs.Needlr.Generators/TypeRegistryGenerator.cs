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
            var referencedAssemblies = DiscoverReferencedAssembliesWithTypeRegistry(compilation)
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
            ReportDisposableCaptiveDependencies(spc, discoveryResult);

            var sourceText = GenerateTypeRegistrySource(discoveryResult, assemblyName, breadcrumbs, projectDirectory, isAotProject);
            spc.AddSource("TypeRegistry.g.cs", SourceText.From(sourceText, Encoding.UTF8));

            var bootstrapText = GenerateModuleInitializerBootstrapSource(assemblyName, referencedAssemblies, breadcrumbs, discoveryResult.Factories.Count > 0, discoveryResult.Options.Count > 0 || discoveryResult.HttpClients.Count > 0, discoveryResult.Providers.Count > 0);
            spc.AddSource("NeedlrSourceGenBootstrap.g.cs", SourceText.From(bootstrapText, Encoding.UTF8));

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

            // Generate provider classes if any were discovered
            if (discoveryResult.Providers.Count > 0)
            {
                // Interface-based providers go in the Generated namespace
                var interfaceProviders = discoveryResult.Providers.Where(p => p.IsInterface).ToList();
                if (interfaceProviders.Count > 0)
                {
                    var providersText = GenerateProvidersSource(interfaceProviders, assemblyName, breadcrumbs, projectDirectory);
                    spc.AddSource("Providers.g.cs", SourceText.From(providersText, Encoding.UTF8));
                }

                // Shorthand class providers need to be generated in their original namespace
                var classProviders = discoveryResult.Providers.Where(p => !p.IsInterface && p.IsPartial).ToList();
                foreach (var provider in classProviders)
                {
                    var providerText = GenerateShorthandProviderSource(provider, assemblyName, breadcrumbs, projectDirectory);
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
                var referencedAssemblyTypes = DiscoverReferencedAssemblyTypesForDiagnostics(compilation);
                var diagnosticsText = DiagnosticsGenerator.GenerateDiagnosticsSource(discoveryResult, assemblyName, projectDirectory, diagnosticOptions, referencedAssemblies, referencedAssemblyTypes);
                spc.AddSource("NeedlrDiagnostics.g.cs", SourceText.From(diagnosticsText, Encoding.UTF8));
            }

            // Generate IDE graph export if configured
            if (ShouldExportGraph(configOptions))
            {
                // Discover types from referenced assemblies with [GenerateTypeRegistry] for graph inclusion
                var referencedAssemblyTypesForGraph = DiscoverReferencedAssemblyTypesForGraph(compilation);

                var graphJson = Export.GraphExporter.GenerateGraphJson(
                    discoveryResult,
                    assemblyName,
                    projectDirectory,
                    diagnostics: null,
                    referencedAssemblyTypes: referencedAssemblyTypesForGraph);
                
                // Embed graph as a comment in a generated file so it's accessible
                // The actual JSON is written to obj folder via the generated code
                var graphSourceText = GenerateGraphExportSource(graphJson, assemblyName, breadcrumbs, projectDirectory);
                spc.AddSource("NeedlrGraph.g.cs", SourceText.From(graphSourceText, Encoding.UTF8));
            }
        });
    }

    /// <summary>
    /// Detects disposable captive dependencies using inferred lifetimes from DiscoveryResult.
    /// Reports NDLRGEN022 when a longer-lived service depends on a shorter-lived disposable.
    /// </summary>
    private static void ReportDisposableCaptiveDependencies(SourceProductionContext spc, DiscoveryResult discoveryResult)
    {
        // Build lookup from type name to DiscoveredType for O(1) lifetime lookups
        var typeLookup = new Dictionary<string, DiscoveredType>();
        foreach (var type in discoveryResult.InjectableTypes)
        {
            typeLookup[type.TypeName] = type;
            // Also map by interfaces so we can look up dependencies by interface
            foreach (var iface in type.InterfaceNames)
            {
                // Only add if not already present (first registration wins for interface resolution)
                if (!typeLookup.ContainsKey(iface))
                {
                    typeLookup[iface] = type;
                }
            }
        }

        // Check each injectable type for captive dependencies
        foreach (var type in discoveryResult.InjectableTypes)
        {
            CheckForCaptiveDependencies(spc, type, typeLookup);
        }
    }

    /// <summary>
    /// Checks a single type for captive dependency issues.
    /// </summary>
    private static void CheckForCaptiveDependencies(
        SourceProductionContext spc,
        DiscoveredType type,
        Dictionary<string, DiscoveredType> typeLookup)
    {
        // Skip types with transient lifetime - they can't capture shorter-lived dependencies
        if (type.Lifetime == GeneratorLifetime.Transient)
            return;

        foreach (var param in type.ConstructorParameters)
        {
            // Skip factory patterns that create new instances on demand
            if (IsFactoryPattern(param.TypeName))
                continue;

            // Try to find the dependency in our discovered types
            if (!typeLookup.TryGetValue(param.TypeName, out var dependency))
                continue;

            // Check if the dependency is shorter-lived
            if (!IsShorterLifetime(type.Lifetime, dependency.Lifetime))
                continue;

            // Check if the dependency is disposable
            if (!dependency.IsDisposable)
                continue;

            // Report the captive dependency
            spc.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DisposableCaptiveDependency,
                Location.None,
                type.TypeName,
                GetLifetimeName(type.Lifetime),
                dependency.TypeName,
                GetLifetimeName(dependency.Lifetime)));
        }
    }

    /// <summary>
    /// Checks if a type name represents a factory pattern that creates new instances on demand.
    /// </summary>
    private static bool IsFactoryPattern(string typeName)
    {
        // Func<T> - factory delegate
        if (typeName.StartsWith("System.Func<", StringComparison.Ordinal))
            return true;

        // Lazy<T> - deferred creation
        if (typeName.StartsWith("System.Lazy<", StringComparison.Ordinal))
            return true;

        // IServiceScopeFactory - creates new scopes
        if (typeName == "Microsoft.Extensions.DependencyInjection.IServiceScopeFactory")
            return true;

        // IServiceProvider - resolves services dynamically
        if (typeName == "System.IServiceProvider")
            return true;

        return false;
    }

    /// <summary>
    /// Checks if dependency lifetime is shorter than consumer lifetime.
    /// </summary>
    private static bool IsShorterLifetime(GeneratorLifetime consumer, GeneratorLifetime dependency)
    {
        // Singleton > Scoped > Transient (in terms of lifetime duration)
        // A shorter lifetime means the dependency will be disposed sooner
        return (consumer, dependency) switch
        {
            (GeneratorLifetime.Singleton, GeneratorLifetime.Scoped) => true,
            (GeneratorLifetime.Singleton, GeneratorLifetime.Transient) => true,
            (GeneratorLifetime.Scoped, GeneratorLifetime.Transient) => true,
            _ => false
        };
    }

    /// <summary>
    /// Gets the human-readable name for a lifetime.
    /// </summary>
    private static string GetLifetimeName(GeneratorLifetime lifetime) => lifetime switch
    {
        GeneratorLifetime.Singleton => "Singleton",
        GeneratorLifetime.Scoped => "Scoped",
        GeneratorLifetime.Transient => "Transient",
        _ => lifetime.ToString()
    };
    
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
    /// Generates source code that writes the graph JSON to a file at build time.
    /// The graph is embedded in the source and written via a module initializer.
    /// </summary>
    private static string GenerateGraphExportSource(string graphJson, string assemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var sb = new StringBuilder();
        
        breadcrumbs.WriteFileHeader(sb, assemblyName, "Needlr IDE Graph Export");
        
        sb.AppendLine("using System;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine();
        sb.AppendLine($"namespace {assemblyName}.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Provides the Needlr dependency graph for IDE tooling.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static class NeedlrGraphExport");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets the dependency graph JSON.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static string GraphJson => GraphJsonContent;");
        sb.AppendLine();
        sb.AppendLine("        private const string GraphJsonContent = @\"");
        
        // Escape the JSON for C# verbatim string (double quotes only)
        var escapedJson = graphJson.Replace("\"", "\"\"");
        sb.Append(escapedJson);
        
        sb.AppendLine("\";");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Writes the graph to the specified path.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static void WriteGraphToFile(string path)");
        sb.AppendLine("        {");
        sb.AppendLine("            File.WriteAllText(path, GraphJson);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
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
            ExpandOpenDecorators(injectableTypes, openDecorators, decorators);
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

        if (hasConfigBoundRegistrations)
        {
            builder.AppendLine();
            GenerateRegisterOptionsMethod(builder, discoveryResult.Options, discoveryResult.HttpClients, safeAssemblyName, breadcrumbs, projectDirectory, isAotProject);
        }

        if (discoveryResult.Providers.Count > 0)
        {
            builder.AppendLine();
            GenerateRegisterProvidersMethod(builder, discoveryResult.Providers, safeAssemblyName, breadcrumbs, projectDirectory);
        }

        builder.AppendLine();
        GenerateApplyDecoratorsMethod(builder, discoveryResult.Decorators, discoveryResult.InterceptedServices.Count > 0, discoveryResult.HostedServices.Count > 0, safeAssemblyName, breadcrumbs, projectDirectory);

        if (discoveryResult.HostedServices.Count > 0)
        {
            builder.AppendLine();
            GenerateRegisterHostedServicesMethod(builder, discoveryResult.HostedServices, breadcrumbs, projectDirectory);
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string GenerateModuleInitializerBootstrapSource(string assemblyName, IReadOnlyList<string> referencedAssemblies, BreadcrumbWriter breadcrumbs, bool hasFactories, bool hasOptions, bool hasProviders)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = GeneratorHelpers.SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Source-Gen Bootstrap");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.Runtime.CompilerServices;");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.Extensions.Configuration;");
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
        
        // Generate the decorator/factory/provider applier lambda
        if (hasFactories || hasProviders)
        {
            builder.AppendLine("            services =>");
            builder.AppendLine("            {");
            builder.AppendLine($"                global::{safeAssemblyName}.Generated.TypeRegistry.ApplyDecorators((IServiceCollection)services);");
            if (hasFactories)
            {
                builder.AppendLine($"                global::{safeAssemblyName}.Generated.FactoryRegistrations.RegisterFactories((IServiceCollection)services);");
            }
            if (hasProviders)
            {
                builder.AppendLine($"                global::{safeAssemblyName}.Generated.TypeRegistry.RegisterProviders((IServiceCollection)services);");
            }
            builder.AppendLine("            },");
        }
        else
        {
            builder.AppendLine($"            services => global::{safeAssemblyName}.Generated.TypeRegistry.ApplyDecorators((IServiceCollection)services),");
        }
        
        // Generate the options registrar lambda for NeedlrSourceGenBootstrap (for backward compat)
        if (hasOptions)
        {
            builder.AppendLine($"            (services, config) => global::{safeAssemblyName}.Generated.TypeRegistry.RegisterOptions((IServiceCollection)services, (IConfiguration)config));");
        }
        else
        {
            builder.AppendLine("            null);");
        }
        
        // Also register with SourceGenRegistry (for ConfiguredSyringe without Generators.Attributes dependency)
        if (hasOptions)
        {
            builder.AppendLine();
            builder.AppendLine("        // Register options with core SourceGenRegistry for ConfiguredSyringe");
            builder.AppendLine($"        global::NexusLabs.Needlr.SourceGenRegistry.RegisterOptionsRegistrar(");
            builder.AppendLine($"            (services, config) => global::{safeAssemblyName}.Generated.TypeRegistry.RegisterOptions((IServiceCollection)services, (IConfiguration)config));");
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
                var safeRefAssemblyName = GeneratorHelpers.SanitizeIdentifier(referencedAssembly);
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
                            ? $"sp.GetRequiredKeyedService<{p.TypeName}>(\"{GeneratorHelpers.EscapeStringLiteral(p.ServiceKey!)}\")"
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
                    builder.Append(string.Join(", ", type.ServiceKeys.Select(k => $"\"{GeneratorHelpers.EscapeStringLiteral(k)}\"")));
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

    private static void GenerateApplyDecoratorsMethod(StringBuilder builder, IReadOnlyList<DiscoveredDecorator> decorators, bool hasInterceptors, bool hasHostedServices, string safeAssemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Applies all discovered decorators, interceptors, and hosted services to the service collection.");
        builder.AppendLine("    /// Decorators are applied in order, with lower Order values applied first (closer to the original service).");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <param name=\"services\">The service collection to apply decorators to.</param>");
        builder.AppendLine("    public static void ApplyDecorators(IServiceCollection services)");
        builder.AppendLine("    {");
        
        // Register ServiceCatalog first
        breadcrumbs.WriteInlineComment(builder, "        ", "Register service catalog for DI resolution");
        builder.AppendLine($"        services.AddSingleton<global::NexusLabs.Needlr.Catalog.IServiceCatalog, global::{safeAssemblyName}.Generated.ServiceCatalog>();");
        builder.AppendLine();

        // Register hosted services first (before decorators apply)
        if (hasHostedServices)
        {
            breadcrumbs.WriteInlineComment(builder, "        ", "Register hosted services");
            builder.AppendLine("        RegisterHostedServices(services);");
            if (decorators.Count > 0 || hasInterceptors)
            {
                builder.AppendLine();
            }
        }

        if (decorators.Count == 0 && !hasInterceptors)
        {
            if (!hasHostedServices)
            {
                breadcrumbs.WriteInlineComment(builder, "        ", "No decorators, interceptors, or hosted services discovered");
            }
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

    private static void GenerateRegisterProvidersMethod(StringBuilder builder, IReadOnlyList<DiscoveredProvider> providers, string safeAssemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Registers all generated providers as Singletons.");
        builder.AppendLine("    /// Providers are strongly-typed service locators that expose services via typed properties.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <param name=\"services\">The service collection to register to.</param>");
        builder.AppendLine("    public static void RegisterProviders(IServiceCollection services)");
        builder.AppendLine("    {");

        foreach (var provider in providers)
        {
            var shortName = provider.SimpleTypeName;
            var sourcePath = provider.SourceFilePath != null
                ? BreadcrumbWriter.GetRelativeSourcePath(provider.SourceFilePath, projectDirectory)
                : $"[{provider.AssemblyName}]";

            breadcrumbs.WriteInlineComment(builder, "        ", $"Provider: {shortName} ← {sourcePath}");

            if (provider.IsInterface)
            {
                // Interface mode: register the generated implementation
                var implName = provider.ImplementationTypeName;
                builder.AppendLine($"        services.AddSingleton<{provider.TypeName}, global::{safeAssemblyName}.Generated.{implName}>();");
            }
            else if (provider.IsPartial)
            {
                // Shorthand class mode: register the partial class as its generated interface
                var interfaceName = provider.InterfaceTypeName;
                var providerNamespace = GetNamespaceFromTypeName(provider.TypeName);
                builder.AppendLine($"        services.AddSingleton<global::{providerNamespace}.{interfaceName}, {provider.TypeName}>();");
            }
        }

        builder.AppendLine("    }");
    }

    private static void GenerateRegisterHostedServicesMethod(StringBuilder builder, IReadOnlyList<DiscoveredHostedService> hostedServices, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Registers all discovered hosted services (BackgroundService and IHostedService implementations).");
        builder.AppendLine("    /// Each service is registered as singleton and also as IHostedService for the host to discover.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <param name=\"services\">The service collection to register to.</param>");
        builder.AppendLine("    private static void RegisterHostedServices(IServiceCollection services)");
        builder.AppendLine("    {");

        foreach (var hostedService in hostedServices)
        {
            var typeName = hostedService.TypeName;
            var shortName = typeName.Split('.').Last();
            var sourcePath = hostedService.SourceFilePath != null
                ? BreadcrumbWriter.GetRelativeSourcePath(hostedService.SourceFilePath, projectDirectory)
                : $"[{hostedService.AssemblyName}]";

            breadcrumbs.WriteInlineComment(builder, "        ", $"Hosted service: {shortName} ← {sourcePath}");

            // Register the concrete type as singleton
            builder.AppendLine($"        services.AddSingleton<{typeName}>();");

            // Register as IHostedService that forwards to the concrete type
            builder.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Hosting.IHostedService>(sp => sp.GetRequiredService<{typeName}>());");
        }

        builder.AppendLine("    }");
    }



    private static string GenerateInterceptorProxiesSource(IReadOnlyList<DiscoveredInterceptedService> interceptedServices, string assemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = GeneratorHelpers.SanitizeIdentifier(assemblyName);

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
            CodeGen.InterceptorCodeGenerator.GenerateInterceptorProxyClass(builder, service, breadcrumbs, projectDirectory);
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
            var proxyTypeName = GeneratorHelpers.GetProxyTypeName(service.TypeName);
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
        var safeAssemblyName = GeneratorHelpers.SanitizeIdentifier(assemblyName);

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
                CodeGen.FactoryCodeGenerator.GenerateFactoryInterface(builder, factory, breadcrumbs, projectDirectory);
                builder.AppendLine();
                CodeGen.FactoryCodeGenerator.GenerateFactoryImplementation(builder, factory, breadcrumbs, projectDirectory);
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
                    CodeGen.FactoryCodeGenerator.GenerateFuncRegistration(builder, factory, ctor, "        ");
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

    private static string GenerateProvidersSource(IReadOnlyList<DiscoveredProvider> providers, string assemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = GeneratorHelpers.SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Generated Providers");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();

        // Generate provider implementations (interface-based only)
        foreach (var provider in providers)
        {
            CodeGen.ProviderCodeGenerator.GenerateProviderImplementation(builder, provider, $"{safeAssemblyName}.Generated", breadcrumbs, projectDirectory);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string GenerateShorthandProviderSource(DiscoveredProvider provider, string assemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var builder = new StringBuilder();
        var providerNamespace = GetNamespaceFromTypeName(provider.TypeName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, $"Needlr Generated Provider: {provider.SimpleTypeName}");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine();
        builder.AppendLine($"namespace {providerNamespace};");
        builder.AppendLine();

        CodeGen.ProviderCodeGenerator.GenerateProviderInterfaceAndPartialClass(builder, provider, providerNamespace, breadcrumbs, projectDirectory);

        return builder.ToString();
    }

    private static string GetNamespaceFromTypeName(string fullyQualifiedName)
    {
        var name = fullyQualifiedName;
        if (name.StartsWith("global::"))
        {
            name = name.Substring(8);
        }

        var lastDot = name.LastIndexOf('.');
        return lastDot >= 0 ? name.Substring(0, lastDot) : string.Empty;
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
                    var openGenericBaseName = GeneratorHelpers.GetGenericBaseName(openGenericName);
                    var interfaceBaseName = GeneratorHelpers.GetGenericBaseName(interfaceName);
                    
                    if (openGenericBaseName == interfaceBaseName)
                    {
                        // This interface is a closed version of the open generic
                        // Create a closed decorator registration
                        var closedDecoratorTypeName = GeneratorHelpers.CreateClosedGenericType(
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
                    if (InterceptorDiscoveryHelper.HasInterceptAttributes(typeSymbol))
                    {
                        interceptedServiceNames.Add(typeSymbol.Name);
                    }
                }
                
                foreach (var typeSymbol in TypeDiscoveryHelper.GetAllTypes(assemblySymbol.GlobalNamespace))
                {
                    // Check if it's a registerable type (injectable, plugin, factory source, or interceptor)
                    var hasFactoryAttr = FactoryDiscoveryHelper.HasGenerateFactoryAttribute(typeSymbol);
                    var hasInterceptAttr = InterceptorDiscoveryHelper.HasInterceptAttributes(typeSymbol);
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
                                      OpenDecoratorDiscoveryHelper.HasOpenDecoratorForAttribute(typeSymbol);
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

    /// <summary>
    /// Discovers types from referenced assemblies with [GenerateTypeRegistry] for graph export.
    /// Unlike the main discovery, this includes internal types since they are registered by their own TypeRegistry.
    /// Returns DiscoveredType objects that can be included in the graph export.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<DiscoveredType>> DiscoverReferencedAssemblyTypesForGraph(Compilation compilation)
    {
        var result = new Dictionary<string, IReadOnlyList<DiscoveredType>>();
        
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                // Skip the current assembly
                if (SymbolEqualityComparer.Default.Equals(assemblySymbol, compilation.Assembly))
                    continue;
                    
                if (!TypeDiscoveryHelper.HasGenerateTypeRegistryAttribute(assemblySymbol))
                    continue;

                // Try to get interface locations from the assembly's ServiceCatalog
                var interfaceLocationLookup = GetInterfaceLocationsFromServiceCatalog(assemblySymbol);

                var assemblyTypes = new List<DiscoveredType>();
                
                foreach (var typeSymbol in TypeDiscoveryHelper.GetAllTypes(assemblySymbol.GlobalNamespace))
                {
                    // Check if it's a registerable type
                    var hasFactoryAttr = FactoryDiscoveryHelper.HasGenerateFactoryAttribute(typeSymbol);
                    
                    // Skip types that are only factories (handled separately)
                    if (hasFactoryAttr)
                        continue;

                    if (!TypeDiscoveryHelper.WouldBeInjectableIgnoringAccessibility(typeSymbol) &&
                        !TypeDiscoveryHelper.WouldBePluginIgnoringAccessibility(typeSymbol))
                        continue;

                    // Skip decorators - they modify other services, not registered directly as services
                    if (TypeDiscoveryHelper.HasDecoratorForAttribute(typeSymbol) || 
                        OpenDecoratorDiscoveryHelper.HasOpenDecoratorForAttribute(typeSymbol))
                        continue;

                    var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                    var interfaceSymbols = TypeDiscoveryHelper.GetRegisterableInterfaces(typeSymbol);
                    var interfaces = interfaceSymbols
                        .Select(i => TypeDiscoveryHelper.GetFullyQualifiedName(i))
                        .ToArray();
                    
                    // Get interface locations from ServiceCatalog lookup, falling back to symbol locations
                    var interfaceInfos = interfaceSymbols.Select(i =>
                    {
                        var ifaceFullName = TypeDiscoveryHelper.GetFullyQualifiedName(i);
                        
                        // First try the ServiceCatalog lookup
                        if (interfaceLocationLookup.TryGetValue(ifaceFullName, out var catalogInfo))
                        {
                            return catalogInfo;
                        }
                        
                        // Fall back to symbol locations (works for source references)
                        var ifaceLocation = i.Locations.FirstOrDefault();
                        var ifaceFilePath = ifaceLocation?.SourceTree?.FilePath;
                        var ifaceLine = ifaceLocation?.GetLineSpan().StartLinePosition.Line + 1 ?? 0;
                        return new InterfaceInfo(ifaceFullName, ifaceFilePath, ifaceLine);
                    }).ToArray();
                    
                    var lifetime = TypeDiscoveryHelper.DetermineLifetime(typeSymbol) ?? GeneratorLifetime.Singleton;
                    var constructorParams = TypeDiscoveryHelper.GetBestConstructorParametersWithKeys(typeSymbol)?.ToArray() 
                        ?? Array.Empty<TypeDiscoveryHelper.ConstructorParameterInfo>();
                    var keyedValues = TypeDiscoveryHelper.GetKeyedServiceKeys(typeSymbol);
                    var sourceFilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;
                    var sourceLine = typeSymbol.Locations.FirstOrDefault() is { } location
                        ? location.GetLineSpan().StartLinePosition.Line + 1
                        : 0;

                    var discoveredType = new DiscoveredType(
                        typeName,
                        interfaces,
                        assemblySymbol.Name,
                        lifetime,
                        constructorParams,
                        keyedValues,
                        sourceFilePath,
                        sourceLine,
                        TypeDiscoveryHelper.IsDisposableType(typeSymbol),
                        interfaceInfos);

                    assemblyTypes.Add(discoveredType);
                }

                if (assemblyTypes.Count > 0)
                {
                    result[assemblySymbol.Name] = assemblyTypes;
                }
            }
        }
        
        return result;
    }

    /// <summary>
    /// Extracts interface location information from a referenced assembly's ServiceCatalog.
    /// The ServiceCatalog is generated by Needlr and contains compile-time interface location data.
    /// </summary>
    private static Dictionary<string, InterfaceInfo> GetInterfaceLocationsFromServiceCatalog(IAssemblySymbol assemblySymbol)
    {
        var result = new Dictionary<string, InterfaceInfo>(StringComparer.Ordinal);
        
        // Look for the ServiceCatalog class in the Generated namespace
        var serviceCatalogTypeName = $"{assemblySymbol.Name}.Generated.ServiceCatalog";
        var serviceCatalogType = assemblySymbol.GetTypeByMetadataName(serviceCatalogTypeName);
        
        if (serviceCatalogType == null)
            return result;
        
        // Find the Services property
        var servicesProperty = serviceCatalogType.GetMembers("Services")
            .OfType<IPropertySymbol>()
            .FirstOrDefault();
        
        if (servicesProperty == null)
            return result;
        
        // The Services property has an initializer with ServiceCatalogEntry array
        // We need to parse the initializer to extract interface locations
        // This requires looking at the declaring syntax reference
        var syntaxRef = servicesProperty.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
            return result;
        
        var syntax = syntaxRef.GetSyntax();
        if (syntax == null)
            return result;
        
        // Parse the array initializer to extract InterfaceEntry data
        // The format is: new InterfaceEntry("fullName", "filePath", line)
        var text = syntax.ToFullString();
        
        // Use regex to extract InterfaceEntry values
        var interfaceEntryPattern = new System.Text.RegularExpressions.Regex(
            @"new\s+global::NexusLabs\.Needlr\.Catalog\.InterfaceEntry\(\s*""([^""]+)""\s*,\s*(""([^""]+)""|null)\s*,\s*(\d+)\s*\)",
            System.Text.RegularExpressions.RegexOptions.Compiled);
        
        foreach (System.Text.RegularExpressions.Match match in interfaceEntryPattern.Matches(text))
        {
            var fullName = match.Groups[1].Value;
            var filePath = match.Groups[3].Success ? match.Groups[3].Value : null;
            var line = int.Parse(match.Groups[4].Value);
            
            if (!result.ContainsKey(fullName))
            {
                result[fullName] = new InterfaceInfo(fullName, filePath, line);
            }
        }
        
        return result;
    }
}
