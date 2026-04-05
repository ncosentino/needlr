using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Aggregated result of type discovery for an assembly.
/// </summary>
internal readonly struct DiscoveryResult
{
    public DiscoveryResult(
        IReadOnlyList<DiscoveredType> injectableTypes,
        IReadOnlyList<DiscoveredPlugin> pluginTypes,
        IReadOnlyList<DiscoveredDecorator> decorators,
        IReadOnlyList<InaccessibleType> inaccessibleTypes,
        IReadOnlyList<MissingTypeRegistryPlugin> missingTypeRegistryPlugins,
        IReadOnlyList<DiscoveredInterceptedService> interceptedServices,
        IReadOnlyList<DiscoveredFactory> factories,
        IReadOnlyList<DiscoveredOptions> options,
        IReadOnlyList<DiscoveredHostedService> hostedServices,
        IReadOnlyList<DiscoveredProvider> providers,
        IReadOnlyList<DiscoveredHttpClient> httpClients)
    {
        InjectableTypes = injectableTypes;
        PluginTypes = pluginTypes;
        Decorators = decorators;
        InaccessibleTypes = inaccessibleTypes;
        MissingTypeRegistryPlugins = missingTypeRegistryPlugins;
        InterceptedServices = interceptedServices;
        Factories = factories;
        Options = options;
        HostedServices = hostedServices;
        Providers = providers;
        HttpClients = httpClients;
    }

    public IReadOnlyList<DiscoveredType> InjectableTypes { get; }
    public IReadOnlyList<DiscoveredPlugin> PluginTypes { get; }
    public IReadOnlyList<DiscoveredDecorator> Decorators { get; }
    public IReadOnlyList<InaccessibleType> InaccessibleTypes { get; }
    public IReadOnlyList<MissingTypeRegistryPlugin> MissingTypeRegistryPlugins { get; }
    public IReadOnlyList<DiscoveredInterceptedService> InterceptedServices { get; }
    public IReadOnlyList<DiscoveredFactory> Factories { get; }
    public IReadOnlyList<DiscoveredOptions> Options { get; }
    public IReadOnlyList<DiscoveredHostedService> HostedServices { get; }
    public IReadOnlyList<DiscoveredProvider> Providers { get; }
    public IReadOnlyList<DiscoveredHttpClient> HttpClients { get; }
}
