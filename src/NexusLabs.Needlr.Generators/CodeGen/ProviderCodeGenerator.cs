// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Text;
using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Generates Provider classes for [Provider] attributed types.
/// </summary>
internal static class ProviderCodeGenerator
{
    /// <summary>
    /// Generates provider implementation for an interface-based provider.
    /// </summary>
    internal static void GenerateProviderImplementation(StringBuilder builder, DiscoveredProvider provider, string generatedNamespace, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var implName = provider.ImplementationTypeName;

        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Strongly-typed service provider implementing <see cref=\"{provider.TypeName}\"/>.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("/// <remarks>");
        builder.AppendLine("/// <para>");
        builder.AppendLine("/// This provider is registered as a <b>Singleton</b>. All service properties are resolved");
        builder.AppendLine("/// at construction time via constructor injection for fail-fast error detection.");
        builder.AppendLine("/// </para>");
        builder.AppendLine("/// <para>");
        builder.AppendLine("/// To create new instances on demand, use factory properties instead of direct service references.");
        builder.AppendLine("/// </para>");
        builder.AppendLine("/// </remarks>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine($"public sealed class {implName} : {provider.TypeName}");
        builder.AppendLine("{");

        // Generate constructor with all properties (Required, Optional, Collection, Factory)
        // Optional parameters must come last in C#
        var injectableProps = provider.Properties
            .OrderBy(p => p.Kind == ProviderPropertyKind.Optional ? 1 : 0)
            .ToList();

        var ctorParams = injectableProps.Select(p =>
        {
            var paramName = GeneratorHelpers.ToCamelCase(p.PropertyName);
            if (p.Kind == ProviderPropertyKind.Optional)
            {
                return $"{p.ServiceTypeName}? {paramName} = null";
            }
            return $"{p.ServiceTypeName} {paramName}";
        });

        var ctorParamList = string.Join(", ", ctorParams);

        builder.AppendLine($"    /// <summary>");
        builder.AppendLine($"    /// Creates a new instance of <see cref=\"{implName}\"/>.");
        builder.AppendLine($"    /// </summary>");
        builder.AppendLine($"    public {implName}({ctorParamList})");
        builder.AppendLine("    {");

        foreach (var prop in injectableProps)
        {
            var paramName = GeneratorHelpers.ToCamelCase(prop.PropertyName);
            builder.AppendLine($"        {prop.PropertyName} = {paramName};");
        }

        builder.AppendLine("    }");
        builder.AppendLine();

        // Generate properties
        foreach (var prop in provider.Properties)
        {
            var propTypeName = prop.Kind == ProviderPropertyKind.Optional
                ? $"{prop.ServiceTypeName}?"
                : prop.ServiceTypeName;

            builder.AppendLine($"    /// <inheritdoc />");
            builder.AppendLine($"    public {propTypeName} {prop.PropertyName} {{ get; }}");
        }

        builder.AppendLine("}");
    }

    /// <summary>
    /// Generates interface and partial class for shorthand [Provider(typeof(T))] on a class.
    /// </summary>
    internal static void GenerateProviderInterfaceAndPartialClass(StringBuilder builder, DiscoveredProvider provider, string generatedNamespace, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var interfaceName = provider.InterfaceTypeName;
        var className = provider.SimpleTypeName;

        // Generate interface
        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Interface for strongly-typed service provider generated from <see cref=\"{provider.TypeName}\"/>.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine($"public interface {interfaceName}");
        builder.AppendLine("{");

        foreach (var prop in provider.Properties)
        {
            var propTypeName = prop.Kind == ProviderPropertyKind.Optional
                ? $"{prop.ServiceTypeName}?"
                : prop.ServiceTypeName;

            builder.AppendLine($"    /// <summary>Gets the {prop.PropertyName} service.</summary>");
            builder.AppendLine($"    {propTypeName} {prop.PropertyName} {{ get; }}");
        }

        builder.AppendLine("}");
        builder.AppendLine();

        // Generate partial class implementation
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Strongly-typed service provider.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("/// <remarks>");
        builder.AppendLine("/// <para>");
        builder.AppendLine("/// This provider is registered as a <b>Singleton</b>. All service properties are resolved");
        builder.AppendLine("/// at construction time via constructor injection for fail-fast error detection.");
        builder.AppendLine("/// </para>");
        builder.AppendLine("/// </remarks>");
        builder.AppendLine($"public partial class {className} : {interfaceName}");
        builder.AppendLine("{");

        // Generate constructor with all properties (Required, Optional, Collection, Factory)
        // Optional parameters must come last in C#
        var injectableProps = provider.Properties
            .OrderBy(p => p.Kind == ProviderPropertyKind.Optional ? 1 : 0)
            .ToList();

        var ctorParams = injectableProps.Select(p =>
        {
            var paramName = GeneratorHelpers.ToCamelCase(p.PropertyName);
            if (p.Kind == ProviderPropertyKind.Optional)
            {
                return $"{p.ServiceTypeName}? {paramName} = null";
            }
            return $"{p.ServiceTypeName} {paramName}";
        });

        var ctorParamList = string.Join(", ", ctorParams);

        builder.AppendLine($"    /// <summary>");
        builder.AppendLine($"    /// Creates a new instance of <see cref=\"{className}\"/>.");
        builder.AppendLine($"    /// </summary>");
        builder.AppendLine($"    public {className}({ctorParamList})");
        builder.AppendLine("    {");

        foreach (var prop in injectableProps)
        {
            var paramName = GeneratorHelpers.ToCamelCase(prop.PropertyName);
            builder.AppendLine($"        {prop.PropertyName} = {paramName};");
        }

        builder.AppendLine("    }");
        builder.AppendLine();

        // Generate properties
        foreach (var prop in provider.Properties)
        {
            var propTypeName = prop.Kind == ProviderPropertyKind.Optional
                ? $"{prop.ServiceTypeName}?"
                : prop.ServiceTypeName;

            builder.AppendLine($"    /// <inheritdoc />");
            builder.AppendLine($"    public {propTypeName} {prop.PropertyName} {{ get; }}");
        }

        builder.AppendLine("}");
    }

    /// <summary>
    /// Generates provider registration code for the TypeRegistry.
    /// </summary>
    internal static void GenerateProviderRegistration(StringBuilder builder, DiscoveredProvider provider, string generatedNamespace)
    {
        var interfaceTypeName = provider.IsInterface
            ? provider.TypeName
            : $"global::{GetNamespace(provider.TypeName)}.{provider.InterfaceTypeName}";

        var implTypeName = provider.IsInterface
            ? $"global::{generatedNamespace}.{provider.ImplementationTypeName}"
            : provider.TypeName;

        builder.AppendLine($"            // Provider: {provider.SimpleTypeName}");
        builder.AppendLine($"            services.AddSingleton<{interfaceTypeName}, {implTypeName}>();");
    }

    private static string GetNamespace(string fullyQualifiedName)
    {
        if (fullyQualifiedName.StartsWith("global::"))
        {
            fullyQualifiedName = fullyQualifiedName.Substring(8);
        }

        var lastDot = fullyQualifiedName.LastIndexOf('.');
        return lastDot >= 0 ? fullyQualifiedName.Substring(0, lastDot) : string.Empty;
    }
}
