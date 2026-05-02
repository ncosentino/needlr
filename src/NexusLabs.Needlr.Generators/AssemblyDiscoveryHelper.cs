// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Discovers types and assemblies from referenced projects that have
/// the <c>[GenerateTypeRegistry]</c> attribute for diagnostics, graph export,
/// and force-loading.
/// </summary>
internal static class AssemblyDiscoveryHelper
{
    /// <summary>
    /// Discovers all referenced assemblies that have the [GenerateTypeRegistry] attribute.
    /// These assemblies need to be force-loaded to ensure their module initializers run.
    /// </summary>
    internal static IReadOnlyList<string> DiscoverReferencedAssembliesWithTypeRegistry(Compilation compilation)
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
    internal static Dictionary<string, List<DiagnosticTypeInfo>> DiscoverReferencedAssemblyTypesForDiagnostics(Compilation compilation)
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
                        !TypeDiscoveryHelper.WouldBePluginIgnoringAccessibility(typeSymbol, compilation.Assembly))
                        continue;

                    var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                    var shortName = typeSymbol.Name;
                    var lifetime = TypeDiscoveryHelper.DetermineLifetime(typeSymbol) ?? GeneratorLifetime.Singleton;
                    var interfaces = TypeDiscoveryHelper.GetRegisterableInterfaces(typeSymbol, compilation.Assembly)
                        .Select(i => TypeDiscoveryHelper.GetFullyQualifiedName(i))
                        .ToArray();
                    var dependencies = TypeDiscoveryHelper.GetBestConstructorParameters(typeSymbol)?
                        .ToArray() ?? Array.Empty<string>();
                    var isDecorator = TypeDiscoveryHelper.HasDecoratorForAttribute(typeSymbol) ||
                                      OpenDecoratorDiscoveryHelper.HasOpenDecoratorForAttribute(typeSymbol);
                    var isPlugin = TypeDiscoveryHelper.WouldBePluginIgnoringAccessibility(typeSymbol, compilation.Assembly);
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
    internal static Dictionary<string, IReadOnlyList<DiscoveredType>> DiscoverReferencedAssemblyTypesForGraph(Compilation compilation)
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
                        !TypeDiscoveryHelper.WouldBePluginIgnoringAccessibility(typeSymbol, compilation.Assembly))
                        continue;

                    // Skip decorators - they modify other services, not registered directly as services
                    if (TypeDiscoveryHelper.HasDecoratorForAttribute(typeSymbol) ||
                        OpenDecoratorDiscoveryHelper.HasOpenDecoratorForAttribute(typeSymbol))
                        continue;

                    var typeName = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
                    var interfaceSymbols = TypeDiscoveryHelper.GetRegisterableInterfaces(typeSymbol, compilation.Assembly);
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
    internal static Dictionary<string, InterfaceInfo> GetInterfaceLocationsFromServiceCatalog(IAssemblySymbol assemblySymbol)
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
