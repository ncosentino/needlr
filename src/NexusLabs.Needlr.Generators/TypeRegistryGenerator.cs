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

            var sourceText = GenerateTypeRegistrySource(discoveryResult, assemblyName, breadcrumbs, projectDirectory, isAotProject);
            spc.AddSource("TypeRegistry.g.cs", SourceText.From(sourceText, Encoding.UTF8));

            // Discover referenced assemblies with [GenerateTypeRegistry] for forced loading
            // Note: Order of force-loading doesn't matter; ordering is applied at service registration time
            var referencedAssemblies = DiscoverReferencedAssembliesWithTypeRegistry(compilation)
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var bootstrapText = GenerateModuleInitializerBootstrapSource(assemblyName, referencedAssemblies, breadcrumbs, discoveryResult.Factories.Count > 0, discoveryResult.Options.Count > 0);
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

            // Generate options validator classes if any have validation methods
            var optionsWithValidators = discoveryResult.Options.Where(o => o.HasValidatorMethod).ToList();
            if (optionsWithValidators.Count > 0)
            {
                var validatorsText = CodeGen.OptionsCodeGenerator.GenerateOptionsValidatorsSource(optionsWithValidators, assemblyName, breadcrumbs, projectDirectory);
                spc.AddSource("OptionsValidators.g.cs", SourceText.From(validatorsText, Encoding.UTF8));
            }

            // Generate parameterless constructors for partial positional records with [Options]
            var optionsNeedingConstructors = discoveryResult.Options.Where(o => o.NeedsGeneratedConstructor).ToList();
            if (optionsNeedingConstructors.Count > 0)
            {
                var constructorsText = CodeGen.OptionsCodeGenerator.GeneratePositionalRecordConstructorsSource(optionsNeedingConstructors, assemblyName, breadcrumbs, projectDirectory);
                spc.AddSource("OptionsConstructors.g.cs", SourceText.From(constructorsText, Encoding.UTF8));
            }

            // Generate diagnostic output files if configured
            var diagnosticOptions = GetDiagnosticOptions(configOptions);
            if (diagnosticOptions.Enabled)
            {
                var referencedAssemblyTypes = DiscoverReferencedAssemblyTypesForDiagnostics(compilation);
                var diagnosticsText = DiagnosticsGenerator.GenerateDiagnosticsSource(discoveryResult, assemblyName, projectDirectory, diagnosticOptions, referencedAssemblies, referencedAssemblyTypes);
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

    /// <summary>
    /// Detects if a type is a positional record (record with primary constructor parameters).
    /// Returns null if not a positional record, or PositionalRecordInfo if it is.
    /// </summary>
    private static PositionalRecordInfo? DetectPositionalRecord(INamedTypeSymbol typeSymbol)
    {
        // Must be a record
        if (!typeSymbol.IsRecord)
            return null;

        // Check for primary constructor with parameters
        // Records with positional parameters have a primary constructor generated from the record declaration
        var primaryCtor = typeSymbol.InstanceConstructors
            .FirstOrDefault(c => c.Parameters.Length > 0 && IsPrimaryConstructor(c, typeSymbol));

        if (primaryCtor == null)
            return null;

        // Check if the record has a parameterless constructor already
        // (user-defined or from record with init-only properties)
        var hasParameterlessCtor = typeSymbol.InstanceConstructors
            .Any(c => c.Parameters.Length == 0 && !c.IsImplicitlyDeclared);

        if (hasParameterlessCtor)
            return null; // Doesn't need generated constructor

        // Check if partial
        var isPartial = typeSymbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
            .Any(s => s.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)));

        // Extract constructor parameters
        var parameters = primaryCtor.Parameters
            .Select(p => new PositionalRecordParameter(p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .ToList();

        // Get namespace
        var containingNamespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? ""
            : typeSymbol.ContainingNamespace.ToDisplayString();

        return new PositionalRecordInfo(
            typeSymbol.Name,
            containingNamespace,
            isPartial,
            parameters);
    }

    /// <summary>
    /// Determines if a constructor is the primary constructor of a record.
    /// Primary constructors for positional records are synthesized and have matching properties.
    /// </summary>
    private static bool IsPrimaryConstructor(IMethodSymbol ctor, INamedTypeSymbol recordType)
    {
        // For positional records, the primary constructor parameters correspond to auto-properties
        // Check if each parameter has a matching property
        foreach (var param in ctor.Parameters)
        {
            var hasMatchingProperty = recordType.GetMembers()
                .OfType<IPropertySymbol>()
                .Any(p => p.Name.Equals(param.Name, StringComparison.Ordinal) &&
                         SymbolEqualityComparer.Default.Equals(p.Type, param.Type));

            if (!hasMatchingProperty)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Extracts bindable properties from an options type for AOT code generation.
    /// </summary>
    private static IReadOnlyList<OptionsPropertyInfo> ExtractBindableProperties(INamedTypeSymbol typeSymbol)
    {
        var properties = new List<OptionsPropertyInfo>();

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol property)
                continue;

            // Skip static, indexers, readonly properties without init
            if (property.IsStatic || property.IsIndexer)
                continue;

            // Must have a setter (set or init)
            if (property.SetMethod == null)
                continue;

            // Check if it's init-only
            var isInitOnly = property.SetMethod.IsInitOnly;

            // Get nullability info
            var isNullable = property.NullableAnnotation == NullableAnnotation.Annotated ||
                             (property.Type is INamedTypeSymbol namedType &&
                              namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

            var typeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Check if it's an enum type
            var isEnum = false;
            string? enumTypeName = null;
            var actualType = property.Type;
            
            // For nullable types, get the underlying type
            if (actualType is INamedTypeSymbol nullableType && 
                nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                nullableType.TypeArguments.Length == 1)
            {
                actualType = nullableType.TypeArguments[0];
            }
            
            if (actualType.TypeKind == TypeKind.Enum)
            {
                isEnum = true;
                enumTypeName = actualType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            properties.Add(new OptionsPropertyInfo(
                property.Name,
                typeName,
                isNullable,
                isInitOnly,
                isEnum,
                enumTypeName));
        }

        return properties;
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
        var inaccessibleTypes = new List<InaccessibleType>();
        var prefixList = namespacePrefixes?.ToList();

        // Collect types from the current compilation if includeSelf is true
        if (includeSelf)
        {
            CollectTypesFromAssembly(compilation.Assembly, prefixList, injectableTypes, pluginTypes, decorators, openDecorators, interceptedServices, factories, options, inaccessibleTypes, compilation, isCurrentAssembly: true);
        }

        // Collect types from all referenced assemblies
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                CollectTypesFromAssembly(assemblySymbol, prefixList, injectableTypes, pluginTypes, decorators, openDecorators, interceptedServices, factories, options, inaccessibleTypes, compilation, isCurrentAssembly: false);
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

        return new DiscoveryResult(injectableTypes, pluginTypes, decorators, inaccessibleTypes, missingTypeRegistryPlugins, interceptedServices, factories, options);
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
            if (OptionsAttributeHelper.HasOptionsAttribute(typeSymbol))
            {
                var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                var optionsAttrs = OptionsAttributeHelper.GetOptionsAttributes(typeSymbol);
                var sourceFilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;

                // Detect positional record (record with primary constructor parameters)
                var positionalRecordInfo = DetectPositionalRecord(typeSymbol);

                // Extract bindable properties for AOT code generation
                var properties = ExtractBindableProperties(typeSymbol);

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

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Type Registry");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        if (hasOptions)
        {
            builder.AppendLine("using Microsoft.Extensions.Configuration;");
            if (isAotProject)
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

        if (hasOptions)
        {
            builder.AppendLine();
            GenerateRegisterOptionsMethod(builder, discoveryResult.Options, safeAssemblyName, breadcrumbs, projectDirectory, isAotProject);
        }

        builder.AppendLine();
        GenerateApplyDecoratorsMethod(builder, discoveryResult.Decorators, discoveryResult.InterceptedServices.Count > 0, safeAssemblyName, breadcrumbs, projectDirectory);

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string GenerateModuleInitializerBootstrapSource(string assemblyName, IReadOnlyList<string> referencedAssemblies, BreadcrumbWriter breadcrumbs, bool hasFactories, bool hasOptions)
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
        
        // Generate the decorator/factory applier lambda
        if (hasFactories)
        {
            builder.AppendLine("            services =>");
            builder.AppendLine("            {");
            builder.AppendLine($"                global::{safeAssemblyName}.Generated.TypeRegistry.ApplyDecorators((IServiceCollection)services);");
            builder.AppendLine($"                global::{safeAssemblyName}.Generated.FactoryRegistrations.RegisterFactories((IServiceCollection)services);");
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

    private static void GenerateRegisterOptionsMethod(StringBuilder builder, IReadOnlyList<DiscoveredOptions> options, string safeAssemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory, bool isAotProject)
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
        else if (isAotProject)
        {
            GenerateAotOptionsRegistration(builder, options, safeAssemblyName, breadcrumbs, projectDirectory);
        }
        else
        {
            GenerateReflectionOptionsRegistration(builder, options, safeAssemblyName, breadcrumbs);
        }

        builder.AppendLine("    }");
    }

    private static void GenerateReflectionOptionsRegistration(StringBuilder builder, IReadOnlyList<DiscoveredOptions> options, string safeAssemblyName, BreadcrumbWriter breadcrumbs)
    {
        // Track external validators to register (avoid duplicates)
        var externalValidatorsToRegister = new HashSet<string>();

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
                    var shortTypeName = GeneratorHelpers.GetShortTypeName(typeName);
                    var validatorClassName = $"global::{safeAssemblyName}.Generated.{shortTypeName}Validator";
                    builder.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Options.IValidateOptions<{typeName}>, {validatorClassName}>();");

                    // If external validator with instance method, register it too
                    if (opt.HasExternalValidator && opt.ValidatorMethod != null && !opt.ValidatorMethod.Value.IsStatic)
                    {
                        externalValidatorsToRegister.Add(opt.ValidatorTypeName!);
                    }
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

        // Register external validators that have instance methods
        foreach (var validatorType in externalValidatorsToRegister)
        {
            builder.AppendLine($"        services.AddSingleton<{validatorType}>();");
        }
    }

    private static void GenerateAotOptionsRegistration(StringBuilder builder, IReadOnlyList<DiscoveredOptions> options, string safeAssemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        breadcrumbs.WriteInlineComment(builder, "        ", "AOT-compatible options binding (no reflection)");

        var externalValidatorsToRegister = new HashSet<string>();

        foreach (var opt in options)
        {
            var typeName = opt.TypeName;
            builder.AppendLine();
            builder.AppendLine($"        // Bind {opt.SectionName} section to {GeneratorHelpers.GetShortTypeName(typeName)}");
            
            if (opt.IsNamed)
            {
                builder.AppendLine($"        services.AddOptions<{typeName}>(\"{opt.Name}\")");
            }
            else
            {
                builder.AppendLine($"        services.AddOptions<{typeName}>()");
            }
            
            builder.AppendLine("            .Configure<IConfiguration>((options, config) =>");
            builder.AppendLine("            {");
            builder.AppendLine($"                var section = config.GetSection(\"{opt.SectionName}\");");
            
            // Generate property binding for each property
            var propIndex = 0;
            foreach (var prop in opt.Properties)
            {
                // Skip init-only properties in AOT mode - they can't be assigned after construction
                if (prop.HasInitOnlySetter)
                {
                    builder.AppendLine($"                // Skipped: {prop.Name} (init-only property cannot be bound in AOT mode)");
                    continue;
                }
                
                GeneratePropertyBinding(builder, prop, propIndex);
                propIndex++;
            }
            
            builder.Append("            })");
            
            // Add validation chain if ValidateOnStart is enabled
            if (opt.ValidateOnStart)
            {
                // Note: ValidateDataAnnotations() uses reflection and is NOT AOT-compatible.
                // Only ValidateOnStart() is emitted. Custom validators still work.
                // TODO: Add NDLRGEN022 warning for DataAnnotations usage in AOT
                builder.AppendLine();
                builder.Append("            .ValidateOnStart()");
            }
            
            builder.AppendLine(";");
            
            // Handle custom validator method registration
            if (opt.ValidateOnStart && opt.HasValidatorMethod)
            {
                var shortTypeName = GeneratorHelpers.GetShortTypeName(typeName);
                var validatorClassName = $"global::{safeAssemblyName}.Generated.{shortTypeName}Validator";
                builder.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Options.IValidateOptions<{typeName}>, {validatorClassName}>();");

                // If external validator with instance method, register it too
                if (opt.HasExternalValidator && opt.ValidatorMethod != null && !opt.ValidatorMethod.Value.IsStatic)
                {
                    externalValidatorsToRegister.Add(opt.ValidatorTypeName!);
                }
            }
        }

        // Register external validators that have instance methods
        foreach (var validatorType in externalValidatorsToRegister)
        {
            builder.AppendLine($"        services.AddSingleton<{validatorType}>();");
        }
    }

    private static void GeneratePropertyBinding(StringBuilder builder, OptionsPropertyInfo prop, int index)
    {
        var varName = $"v{index}";
        
        // Determine how to parse the value based on type
        var typeName = prop.TypeName;
        var isNullableValueType = typeName.StartsWith("global::System.Nullable<") || typeName.EndsWith("?");
        var baseTypeName = GetBaseTypeName(typeName);
        
        builder.Append($"                if (section[\"{prop.Name}\"] is {{ }} {varName}");
        
        // Check if it's an enum first
        if (prop.IsEnum && prop.EnumTypeName != null)
        {
            builder.AppendLine($" && global::System.Enum.TryParse<{prop.EnumTypeName}>({varName}, true, out var p{index})) options.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "string" || baseTypeName == "global::System.String")
        {
            // String: direct assignment
            builder.AppendLine($") options.{prop.Name} = {varName};");
        }
        else if (baseTypeName == "int" || baseTypeName == "global::System.Int32")
        {
            builder.AppendLine($" && int.TryParse({varName}, out var p{index})) options.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "bool" || baseTypeName == "global::System.Boolean")
        {
            builder.AppendLine($" && bool.TryParse({varName}, out var p{index})) options.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "double" || baseTypeName == "global::System.Double")
        {
            builder.AppendLine($" && double.TryParse({varName}, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p{index})) options.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "float" || baseTypeName == "global::System.Single")
        {
            builder.AppendLine($" && float.TryParse({varName}, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p{index})) options.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "decimal" || baseTypeName == "global::System.Decimal")
        {
            builder.AppendLine($" && decimal.TryParse({varName}, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p{index})) options.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "long" || baseTypeName == "global::System.Int64")
        {
            builder.AppendLine($" && long.TryParse({varName}, out var p{index})) options.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "short" || baseTypeName == "global::System.Int16")
        {
            builder.AppendLine($" && short.TryParse({varName}, out var p{index})) options.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "byte" || baseTypeName == "global::System.Byte")
        {
            builder.AppendLine($" && byte.TryParse({varName}, out var p{index})) options.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "char" || baseTypeName == "global::System.Char")
        {
            builder.AppendLine($" && {varName}.Length == 1) options.{prop.Name} = {varName}[0];");
        }
        else if (baseTypeName == "global::System.TimeSpan")
        {
            builder.AppendLine($" && global::System.TimeSpan.TryParse({varName}, out var p{index})) options.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "global::System.DateTime")
        {
            builder.AppendLine($" && global::System.DateTime.TryParse({varName}, out var p{index})) options.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "global::System.DateTimeOffset")
        {
            builder.AppendLine($" && global::System.DateTimeOffset.TryParse({varName}, out var p{index})) options.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "global::System.Guid")
        {
            builder.AppendLine($" && global::System.Guid.TryParse({varName}, out var p{index})) options.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "global::System.Uri")
        {
            builder.AppendLine($" && global::System.Uri.TryCreate({varName}, global::System.UriKind.RelativeOrAbsolute, out var p{index})) options.{prop.Name} = p{index};");
        }
        else
        {
            // Unsupported type - skip silently (matching ConfigurationBinder behavior)
            builder.AppendLine($") {{ }} // Skipped: {typeName} (not a supported primitive)");
        }
    }

    private static string GetBaseTypeName(string typeName)
    {
        // Handle nullable types like "global::System.Nullable<int>" or "int?"
        if (typeName.StartsWith("global::System.Nullable<") && typeName.EndsWith(">"))
        {
            return typeName.Substring("global::System.Nullable<".Length, typeName.Length - "global::System.Nullable<".Length - 1);
        }
        if (typeName.EndsWith("?"))
        {
            return typeName.Substring(0, typeName.Length - 1);
        }
        return typeName;
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
}
