// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Generates decorator registration, hosted service registration,
/// provider registration, and open-decorator expansion code.
/// </summary>
internal static class DecoratorsCodeGenerator
{
    /// <summary>
    /// Emits the <c>ApplyDecorators(IServiceCollection)</c> method body into the TypeRegistry class.
    /// </summary>
    internal static void GenerateApplyDecoratorsMethod(StringBuilder builder, IReadOnlyList<DiscoveredDecorator> decorators, bool hasInterceptors, bool hasHostedServices, string safeAssemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
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

    /// <summary>
    /// Emits the <c>RegisterHostedServices(IServiceCollection)</c> method body.
    /// </summary>
    internal static void GenerateRegisterHostedServicesMethod(StringBuilder builder, IReadOnlyList<DiscoveredHostedService> hostedServices, BreadcrumbWriter breadcrumbs, string? projectDirectory)
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

    /// <summary>
    /// Expands open generic decorators into concrete decorator registrations
    /// for each discovered closed implementation of the open generic interface.
    /// </summary>
    internal static void ExpandOpenDecorators(
        IReadOnlyList<DiscoveredType> injectableTypes,
        IReadOnlyList<DiscoveredOpenDecorator> openDecorators,
        List<DiscoveredDecorator> decorators)
    {
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
    /// Emits the <c>RegisterProviders(IServiceCollection)</c> method body.
    /// </summary>
    internal static void GenerateRegisterProvidersMethod(StringBuilder builder, IReadOnlyList<DiscoveredProvider> providers, string safeAssemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
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
                var providerNamespace = GeneratorHelpers.GetNamespaceFromTypeName(provider.TypeName);
                builder.AppendLine($"        services.AddSingleton<global::{providerNamespace}.{interfaceName}, {provider.TypeName}>();");
            }
        }

        builder.AppendLine("    }");
    }
}
