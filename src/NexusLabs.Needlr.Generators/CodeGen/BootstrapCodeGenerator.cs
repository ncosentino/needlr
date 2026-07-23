// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Generates the <c>NeedlrSourceGenBootstrap.g.cs</c> module initializer file.
/// </summary>
internal static class BootstrapCodeGenerator
{
    /// <summary>
    /// Emits the module-initializer bootstrap source that registers TypeRegistry
    /// callbacks and runs referenced TypeRegistry module constructors.
    /// </summary>
    internal static string GenerateModuleInitializerBootstrapSource(string assemblyName, IReadOnlyList<string> referencedAssemblies, BreadcrumbWriter breadcrumbs, bool hasFactories, bool hasOptions, bool hasProviders)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = GeneratorHelpers.SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Source-Gen Bootstrap");
        builder.AppendLine("#nullable enable");
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

        // Generate the referenced-module initialization call when dependencies have registries.
        if (referencedAssemblies.Count > 0)
        {
            builder.AppendLine("        // Run referenced module constructors so their registries register");
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
            builder.AppendLine("    /// Runs module constructors for referenced assemblies with");
            builder.AppendLine("    /// [GenerateTypeRegistry], ensuring their registries register.");
            builder.AppendLine("    /// </summary>");
            builder.AppendLine("    /// <remarks>");
            builder.AppendLine("    /// A typeof expression alone does not guarantee module initialization.");
            builder.AppendLine("    /// RunModuleConstructor is idempotent when a dependency was already initialized.");
            builder.AppendLine("    /// </remarks>");
            builder.AppendLine("    private static void ForceLoadReferencedAssemblies()");
            builder.AppendLine("    {");

            foreach (var referencedAssembly in referencedAssemblies)
            {
                var safeRefAssemblyName = GeneratorHelpers.SanitizeIdentifier(referencedAssembly);
                builder.AppendLine("        global::System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(");
                builder.AppendLine($"            typeof(global::{safeRefAssemblyName}.Generated.TypeRegistry).Module.ModuleHandle);");
            }

            builder.AppendLine("    }");
        }

        builder.AppendLine("}");

        return builder.ToString();
    }
}
