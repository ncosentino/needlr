using System.Reflection;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Catalog;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

public sealed class RegistrationManifestIntegrationTests
{
    private const string ManifestKey =
        "NexusLabs.Needlr.RegistrationManifest";

    [Fact]
    public void RegistrationManifest_MatchesRuntimeServiceCatalog()
    {
        var provider = new Syringe()
            .UsingGeneratedComponents(
                Generated.TypeRegistry.GetInjectableTypes,
                Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();
        var catalog = provider.GetRequiredService<IServiceCatalog>();

        var manifestJson = Assert.Single(
            typeof(RegistrationManifestIntegrationTests).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>(),
            attribute => attribute.Key == ManifestKey)
            .Value;
        Assert.NotNull(manifestJson);

        using var manifest = JsonDocument.Parse(manifestJson);
        var root = manifest.RootElement;

        Assert.Equal(
            catalog.Services
                .Select(service => service.TypeName)
                .OrderBy(typeName => typeName, StringComparer.Ordinal),
            root.GetProperty("injectableTypes")
                .EnumerateArray()
                .Select(entry =>
                    entry.GetProperty("implementationType").GetString()!)
                .OrderBy(typeName => typeName, StringComparer.Ordinal));
        Assert.Equal(
            catalog.Decorators
                .Select(decorator =>
                    $"{decorator.ServiceTypeName}|{decorator.DecoratorTypeName}|{decorator.Order}")
                .OrderBy(value => value, StringComparer.Ordinal),
            root.GetProperty("decorators")
                .EnumerateArray()
                .Select(entry =>
                    $"{entry.GetProperty("serviceType").GetString()}|" +
                    $"{entry.GetProperty("decoratorType").GetString()}|" +
                    $"{entry.GetProperty("order").GetInt32()}")
                .OrderBy(value => value, StringComparer.Ordinal));
        Assert.Equal(
            catalog.HostedServices
                .Select(service => service.TypeName)
                .OrderBy(typeName => typeName, StringComparer.Ordinal),
            root.GetProperty("hostedServices")
                .EnumerateArray()
                .Select(entry =>
                    entry.GetProperty("implementationType").GetString()!)
                .OrderBy(typeName => typeName, StringComparer.Ordinal));
        Assert.Equal(
            catalog.InterceptedServices
                .Select(service => service.TypeName)
                .OrderBy(typeName => typeName, StringComparer.Ordinal),
            root.GetProperty("interceptedServices")
                .EnumerateArray()
                .Select(entry =>
                    entry.GetProperty("implementationType").GetString()!)
                .OrderBy(typeName => typeName, StringComparer.Ordinal));
        Assert.Equal(
            catalog.Options
                .Select(options => $"{options.TypeName}|{options.Name}")
                .OrderBy(value => value, StringComparer.Ordinal),
            root.GetProperty("options")
                .EnumerateArray()
                .Select(entry =>
                    $"{entry.GetProperty("optionsType").GetString()}|" +
                    $"{entry.GetProperty("name").GetString()}")
                .OrderBy(value => value, StringComparer.Ordinal));
        Assert.Equal(
            catalog.Plugins
                .Select(plugin => plugin.TypeName)
                .OrderBy(typeName => typeName, StringComparer.Ordinal),
            root.GetProperty("plugins")
                .EnumerateArray()
                .Select(entry =>
                    entry.GetProperty("pluginType").GetString()!)
                .OrderBy(typeName => typeName, StringComparer.Ordinal));
    }
}
