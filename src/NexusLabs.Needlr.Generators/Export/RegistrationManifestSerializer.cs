using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.Export;

/// <summary>
/// Serializes the normalized source-generated registration plan for compiler tooling.
/// </summary>
internal static class RegistrationManifestSerializer
{
    internal const string SchemaVersion = "1.0";

    internal static string Serialize(GeneratedRegistrationPlan plan)
    {
        var builder = new StringBuilder();
        var discovery = plan.DiscoveryResult;
        var safeAssemblyName = GeneratorHelpers.SanitizeIdentifier(
            plan.AssemblyName);

        builder.Append('{');
        AppendStringProperty(builder, "schemaVersion", SchemaVersion);
        builder.Append(',');
        AppendStringProperty(builder, "assemblyName", plan.AssemblyName);
        builder.Append(',');
        AppendBooleanProperty(builder, "isAotProject", plan.IsAotProject);
        builder.Append(',');
        AppendServiceCatalogRegistration(
            builder,
            plan.RegistersServiceCatalog,
            safeAssemblyName);
        builder.Append(',');
        AppendStringArrayProperty(
            builder,
            "referencedRegistryAssemblies",
            plan.ReferencedRegistryAssemblies);
        builder.Append(',');
        AppendInjectableTypes(builder, discovery.InjectableTypes);
        builder.Append(',');
        AppendDecorators(builder, discovery.Decorators);
        builder.Append(',');
        AppendHostedServices(builder, discovery.HostedServices);
        builder.Append(',');
        AppendInterceptedServices(
            builder,
            discovery.InterceptedServices,
            safeAssemblyName);
        builder.Append(',');
        AppendFactories(builder, plan.EmittedFactories, safeAssemblyName);
        builder.Append(',');
        AppendProviders(builder, plan.EmittedProviders, safeAssemblyName);
        builder.Append(',');
        AppendOptions(
            builder,
            discovery.Options,
            safeAssemblyName,
            plan.IsAotProject);
        builder.Append(',');
        AppendHttpClients(builder, discovery.HttpClients);
        builder.Append(',');
        AppendComposedRegistrations(
            builder,
            discovery.ComposedRegistrations);
        builder.Append(',');
        AppendPlugins(builder, discovery.PluginTypes);
        builder.Append('}');

        return builder.ToString();
    }

    private static void AppendServiceCatalogRegistration(
        StringBuilder builder,
        bool registersServiceCatalog,
        string safeAssemblyName)
    {
        AppendPropertyName(builder, "serviceCatalogRegistration");
        if (!registersServiceCatalog)
        {
            builder.Append("null");
            return;
        }

        builder.Append('{');
        AppendStringProperty(
            builder,
            "serviceType",
            "global::NexusLabs.Needlr.Catalog.IServiceCatalog");
        builder.Append(',');
        AppendStringProperty(
            builder,
            "implementationType",
            $"global::{safeAssemblyName}.Generated.ServiceCatalog");
        builder.Append(',');
        AppendStringProperty(builder, "lifetime", "Singleton");
        builder.Append('}');
    }

    private static void AppendInjectableTypes(
        StringBuilder builder,
        IReadOnlyList<DiscoveredType> injectableTypes)
    {
        AppendPropertyName(builder, "injectableTypes");
        builder.Append('[');

        var orderedTypes = injectableTypes
            .OrderBy(type => type.AssemblyName, StringComparer.Ordinal)
            .ThenBy(type => type.TypeName, StringComparer.Ordinal)
            .ToArray();

        for (var index = 0; index < orderedTypes.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var type = orderedTypes[index];
            builder.Append('{');
            AppendStringProperty(
                builder,
                "implementationType",
                type.TypeName);
            builder.Append(',');
            AppendStringProperty(builder, "assemblyName", type.AssemblyName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "lifetime",
                type.Lifetime.ToString());
            builder.Append(',');
            AppendServiceTypesProperty(
                builder,
                type.TypeName,
                type.InterfaceNames);
            builder.Append(',');
            AppendSortedStringArrayProperty(
                builder,
                "serviceKeys",
                type.ServiceKeys);
            builder.Append(',');
            AppendConstructorParameters(
                builder,
                "constructorParameters",
                type.ConstructorParameters);
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendDecorators(
        StringBuilder builder,
        IReadOnlyList<DiscoveredDecorator> decorators)
    {
        AppendPropertyName(builder, "decorators");
        builder.Append('[');

        var orderedDecorators = decorators
            .GroupBy(decorator => decorator.ServiceTypeName)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .SelectMany(group => group.OrderBy(decorator => decorator.Order))
            .ToArray();

        for (var index = 0; index < orderedDecorators.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var decorator = orderedDecorators[index];
            builder.Append('{');
            AppendStringProperty(
                builder,
                "serviceType",
                decorator.ServiceTypeName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "decoratorType",
                decorator.DecoratorTypeName);
            builder.Append(',');
            AppendNumberProperty(builder, "order", decorator.Order);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "assemblyName",
                decorator.AssemblyName);
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendHostedServices(
        StringBuilder builder,
        IReadOnlyList<DiscoveredHostedService> hostedServices)
    {
        AppendPropertyName(builder, "hostedServices");
        builder.Append('[');

        for (var index = 0; index < hostedServices.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var service = hostedServices[index];
            builder.Append('{');
            AppendStringProperty(
                builder,
                "implementationType",
                service.TypeName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "assemblyName",
                service.AssemblyName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "lifetime",
                service.Lifetime.ToString());
            builder.Append(',');
            AppendConstructorParameters(
                builder,
                "constructorParameters",
                service.ConstructorParameters);
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendInterceptedServices(
        StringBuilder builder,
        IReadOnlyList<DiscoveredInterceptedService> interceptedServices,
        string safeAssemblyName)
    {
        AppendPropertyName(builder, "interceptedServices");
        builder.Append('[');

        for (var index = 0; index < interceptedServices.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var service = interceptedServices[index];
            builder.Append('{');
            AppendStringProperty(
                builder,
                "implementationType",
                service.TypeName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "proxyType",
                $"global::{safeAssemblyName}.Generated." +
                    GeneratorHelpers.GetProxyTypeName(service.TypeName));
            builder.Append(',');
            AppendStringProperty(
                builder,
                "assemblyName",
                service.AssemblyName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "lifetime",
                service.Lifetime.ToString());
            builder.Append(',');
            AppendSortedStringArrayProperty(
                builder,
                "serviceTypes",
                service.InterfaceNames);
            builder.Append(',');
            AppendSortedStringArrayProperty(
                builder,
                "interceptorTypes",
                service.AllInterceptorTypeNames);
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendFactories(
        StringBuilder builder,
        IReadOnlyList<DiscoveredFactory> factories,
        string safeAssemblyName)
    {
        AppendPropertyName(builder, "factories");
        builder.Append('[');

        for (var index = 0; index < factories.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var factory = factories[index];
            builder.Append('{');
            AppendStringProperty(builder, "targetType", factory.TypeName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "returnType",
                factory.ReturnTypeName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "assemblyName",
                factory.AssemblyName);
            builder.Append(',');
            AppendBooleanProperty(
                builder,
                "generatesFunc",
                factory.GenerateFunc);
            builder.Append(',');
            AppendBooleanProperty(
                builder,
                "generatesInterface",
                factory.GenerateInterface);
            builder.Append(',');
            AppendNullableStringProperty(
                builder,
                "generatedFactoryServiceType",
                factory.GenerateInterface
                    ? $"global::{safeAssemblyName}.Generated.I" +
                        $"{factory.SimpleTypeName}Factory"
                    : null);
            builder.Append(',');
            AppendNullableStringProperty(
                builder,
                "generatedFactoryImplementationType",
                factory.GenerateInterface
                    ? $"global::{safeAssemblyName}.Generated." +
                        $"{factory.SimpleTypeName}Factory"
                    : null);
            builder.Append(',');
            AppendFactoryConstructors(builder, factory.Constructors);
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendFactoryConstructors(
        StringBuilder builder,
        IReadOnlyList<FactoryDiscoveryHelper.FactoryConstructorInfo> constructors)
    {
        AppendPropertyName(builder, "constructors");
        builder.Append('[');

        for (var index = 0; index < constructors.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var constructor = constructors[index];
            builder.Append('{');
            AppendConstructorParameters(
                builder,
                "injectableParameters",
                constructor.InjectableParameters);
            builder.Append(',');
            AppendConstructorParameters(
                builder,
                "runtimeParameters",
                constructor.RuntimeParameters);
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendProviders(
        StringBuilder builder,
        IReadOnlyList<DiscoveredProvider> providers,
        string safeAssemblyName)
    {
        AppendPropertyName(builder, "providers");
        builder.Append('[');

        for (var index = 0; index < providers.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var provider = providers[index];
            var providerNamespace =
                GeneratorHelpers.GetNamespaceFromTypeName(provider.TypeName);
            var serviceType = provider.IsInterface
                ? provider.TypeName
                : $"global::{providerNamespace}.{provider.InterfaceTypeName}";
            var implementationType = provider.IsInterface
                ? $"global::{safeAssemblyName}.Generated." +
                    provider.ImplementationTypeName
                : provider.TypeName;

            builder.Append('{');
            AppendStringProperty(builder, "sourceType", provider.TypeName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "assemblyName",
                provider.AssemblyName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "mode",
                provider.IsInterface ? "interface" : "shorthand");
            builder.Append(',');
            AppendStringProperty(builder, "serviceType", serviceType);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "implementationType",
                implementationType);
            builder.Append(',');
            AppendProviderProperties(builder, provider.Properties);
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendProviderProperties(
        StringBuilder builder,
        IReadOnlyList<ProviderPropertyInfo> properties)
    {
        AppendPropertyName(builder, "properties");
        builder.Append('[');

        for (var index = 0; index < properties.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var property = properties[index];
            builder.Append('{');
            AppendStringProperty(builder, "name", property.PropertyName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "serviceType",
                property.ServiceTypeName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "resolutionKind",
                property.Kind.ToString());
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendOptions(
        StringBuilder builder,
        IReadOnlyList<DiscoveredOptions> options,
        string safeAssemblyName,
        bool isAotProject)
    {
        AppendPropertyName(builder, "options");
        builder.Append('[');

        for (var index = 0; index < options.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var option = options[index];
            var shortTypeName =
                GeneratorHelpers.GetShortTypeName(option.TypeName);
            var registersGeneratedValidator =
                option.ValidateOnStart && option.HasValidatorMethod;
            var registersDataAnnotationsValidator =
                option.HasDataAnnotations &&
                (isAotProject
                    ? !option.RequiresFactoryPattern ||
                        option.ValidateOnStart
                    : option.ValidateOnStart);

            builder.Append('{');
            AppendStringProperty(builder, "optionsType", option.TypeName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "assemblyName",
                option.AssemblyName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "sectionName",
                option.SectionName);
            builder.Append(',');
            AppendNullableStringProperty(builder, "name", option.Name);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "bindingMode",
                GetOptionsBindingMode(option, isAotProject));
            builder.Append(',');
            AppendBooleanProperty(
                builder,
                "validateOnStart",
                option.ValidateOnStart);
            builder.Append(',');
            AppendNullableStringProperty(
                builder,
                "generatedValidatorType",
                registersGeneratedValidator
                    ? $"global::{safeAssemblyName}.Generated." +
                        $"{shortTypeName}Validator"
                    : null);
            builder.Append(',');
            AppendNullableStringProperty(
                builder,
                "generatedDataAnnotationsValidatorType",
                registersDataAnnotationsValidator
                    ? $"global::{safeAssemblyName}.Generated." +
                        $"{shortTypeName}DataAnnotationsValidator"
                    : null);
            builder.Append(',');
            AppendNullableStringProperty(
                builder,
                "externalValidatorType",
                registersGeneratedValidator &&
                    option.HasExternalValidator &&
                    option.ValidatorMethod is { IsStatic: false }
                    ? option.ValidatorTypeName
                    : null);
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static string GetOptionsBindingMode(
        DiscoveredOptions options,
        bool isAotProject)
    {
        if (!isAotProject)
        {
            return "reflection";
        }

        if (options.IsPositionalRecord)
        {
            return "aot-positional-record";
        }

        return options.HasInitOnlyProperties
            ? "aot-init-only"
            : "aot-configure";
    }

    private static void AppendHttpClients(
        StringBuilder builder,
        IReadOnlyList<DiscoveredHttpClient> httpClients)
    {
        AppendPropertyName(builder, "httpClients");
        builder.Append('[');

        for (var index = 0; index < httpClients.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var client = httpClients[index];
            builder.Append('{');
            AppendStringProperty(
                builder,
                "optionsType",
                client.TypeName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "assemblyName",
                client.AssemblyName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "clientName",
                client.ClientName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "sectionName",
                client.SectionName);
            builder.Append(',');
            AppendHttpClientCapabilities(builder, client.Capabilities);
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendHttpClientCapabilities(
        StringBuilder builder,
        HttpClientCapabilities capabilities)
    {
        AppendPropertyName(builder, "capabilities");
        builder.Append('[');

        var separator = false;
        AppendCapability(
            builder,
            capabilities,
            HttpClientCapabilities.Timeout,
            "Timeout",
            ref separator);
        AppendCapability(
            builder,
            capabilities,
            HttpClientCapabilities.UserAgent,
            "UserAgent",
            ref separator);
        AppendCapability(
            builder,
            capabilities,
            HttpClientCapabilities.BaseAddress,
            "BaseAddress",
            ref separator);
        AppendCapability(
            builder,
            capabilities,
            HttpClientCapabilities.Headers,
            "Headers",
            ref separator);

        builder.Append(']');
    }

    private static void AppendCapability(
        StringBuilder builder,
        HttpClientCapabilities capabilities,
        HttpClientCapabilities capability,
        string name,
        ref bool separator)
    {
        if ((capabilities & capability) == 0)
        {
            return;
        }

        if (separator)
        {
            builder.Append(',');
        }

        AppendString(builder, name);
        separator = true;
    }

    private static void AppendComposedRegistrations(
        StringBuilder builder,
        IReadOnlyList<DiscoveredComposedRegistration> registrations)
    {
        AppendPropertyName(builder, "composedRegistrations");
        builder.Append('[');

        for (var index = 0; index < registrations.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var registration = registrations[index];
            builder.Append('{');
            AppendStringProperty(
                builder,
                "serviceType",
                registration.FacadeTypeName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "implementationType",
                registration.ClosedCompositionTypeName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "assemblyName",
                registration.AssemblyName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "lifetime",
                registration.Lifetime.ToString());
            builder.Append(',');
            AppendStringArrayProperty(
                builder,
                "constructorArguments",
                registration.ConstructorArguments);
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendPlugins(
        StringBuilder builder,
        IReadOnlyList<DiscoveredPlugin> plugins)
    {
        AppendPropertyName(builder, "plugins");
        builder.Append('[');

        var orderedPlugins = plugins
            .GroupBy(plugin => plugin.AssemblyName)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .SelectMany(group => group
                .OrderBy(plugin => plugin.Order)
                .ThenBy(plugin => plugin.TypeName, StringComparer.Ordinal))
            .ToArray();

        for (var index = 0; index < orderedPlugins.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var plugin = orderedPlugins[index];
            builder.Append('{');
            AppendStringProperty(builder, "pluginType", plugin.TypeName);
            builder.Append(',');
            AppendStringProperty(
                builder,
                "assemblyName",
                plugin.AssemblyName);
            builder.Append(',');
            AppendNumberProperty(builder, "order", plugin.Order);
            builder.Append(',');
            AppendSortedStringArrayProperty(
                builder,
                "pluginInterfaces",
                plugin.InterfaceNames);
            builder.Append(',');
            AppendSortedStringArrayProperty(
                builder,
                "attributeTypes",
                plugin.AttributeNames);
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendServiceTypesProperty(
        StringBuilder builder,
        string implementationType,
        IReadOnlyList<string> interfaceTypes)
    {
        AppendPropertyName(builder, "serviceTypes");
        builder.Append('[');
        AppendString(builder, implementationType);

        foreach (var interfaceType in interfaceTypes.OrderBy(
            value => value,
            StringComparer.Ordinal))
        {
            builder.Append(',');
            AppendString(builder, interfaceType);
        }

        builder.Append(']');
    }

    private static void AppendConstructorParameters(
        StringBuilder builder,
        string propertyName,
        IReadOnlyList<TypeDiscoveryHelper.ConstructorParameterInfo> parameters)
    {
        AppendPropertyName(builder, propertyName);
        builder.Append('[');

        for (var index = 0; index < parameters.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var parameter = parameters[index];
            builder.Append('{');
            AppendNullableStringProperty(
                builder,
                "name",
                parameter.ParameterName);
            builder.Append(',');
            AppendStringProperty(builder, "type", parameter.TypeName);
            builder.Append(',');
            AppendNullableStringProperty(
                builder,
                "serviceKey",
                parameter.ServiceKey);
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void AppendStringArrayProperty(
        StringBuilder builder,
        string propertyName,
        IReadOnlyList<string> values)
    {
        AppendPropertyName(builder, propertyName);
        builder.Append('[');

        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            AppendString(builder, values[index]);
        }

        builder.Append(']');
    }

    private static void AppendSortedStringArrayProperty(
        StringBuilder builder,
        string propertyName,
        IEnumerable<string> values)
    {
        AppendStringArrayProperty(
            builder,
            propertyName,
            values.OrderBy(
                value => value,
                StringComparer.Ordinal)
                .ToArray());
    }

    private static void AppendStringProperty(
        StringBuilder builder,
        string propertyName,
        string value)
    {
        AppendPropertyName(builder, propertyName);
        AppendString(builder, value);
    }

    private static void AppendNullableStringProperty(
        StringBuilder builder,
        string propertyName,
        string? value)
    {
        AppendPropertyName(builder, propertyName);
        if (value is null)
        {
            builder.Append("null");
            return;
        }

        AppendString(builder, value);
    }

    private static void AppendBooleanProperty(
        StringBuilder builder,
        string propertyName,
        bool value)
    {
        AppendPropertyName(builder, propertyName);
        builder.Append(value ? "true" : "false");
    }

    private static void AppendNumberProperty(
        StringBuilder builder,
        string propertyName,
        int value)
    {
        AppendPropertyName(builder, propertyName);
        builder.Append(GeneratorHelpers.Literal(value));
    }

    private static void AppendPropertyName(
        StringBuilder builder,
        string propertyName)
    {
        AppendString(builder, propertyName);
        builder.Append(':');
    }

    private static void AppendString(StringBuilder builder, string value)
    {
        const string Hex = "0123456789abcdef";

        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character < ' ' ||
                        char.IsSurrogate(character) ||
                        character == '\u0085' ||
                        character == '\u2028' ||
                        character == '\u2029')
                    {
                        AppendUnicodeEscape(builder, character, Hex);
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }
        builder.Append('"');
    }

    private static void AppendUnicodeEscape(
        StringBuilder builder,
        char character,
        string hex)
    {
        builder.Append("\\u");
        builder.Append(hex[(character >> 12) & 0x0f]);
        builder.Append(hex[(character >> 8) & 0x0f]);
        builder.Append(hex[(character >> 4) & 0x0f]);
        builder.Append(hex[character & 0x0f]);
    }
}
