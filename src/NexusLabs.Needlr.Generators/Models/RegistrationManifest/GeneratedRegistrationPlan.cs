using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// The normalized source-generation inputs that determine emitted registration behavior.
/// </summary>
internal readonly struct GeneratedRegistrationPlan
{
    public GeneratedRegistrationPlan(
        string assemblyName,
        bool isAotProject,
        bool registersServiceCatalog,
        IReadOnlyList<string> referencedRegistryAssemblies,
        IReadOnlyList<DiscoveredFactory> emittedFactories,
        IReadOnlyList<DiscoveredProvider> emittedProviders,
        DiscoveryResult discoveryResult)
    {
        AssemblyName = assemblyName;
        IsAotProject = isAotProject;
        RegistersServiceCatalog = registersServiceCatalog;
        ReferencedRegistryAssemblies = referencedRegistryAssemblies;
        EmittedFactories = emittedFactories;
        EmittedProviders = emittedProviders;
        DiscoveryResult = discoveryResult;
    }

    public string AssemblyName { get; }
    public bool IsAotProject { get; }
    public bool RegistersServiceCatalog { get; }
    public IReadOnlyList<string> ReferencedRegistryAssemblies { get; }
    public IReadOnlyList<DiscoveredFactory> EmittedFactories { get; }
    public IReadOnlyList<DiscoveredProvider> EmittedProviders { get; }
    public DiscoveryResult DiscoveryResult { get; }

    public bool HasFactoryRegistrations => EmittedFactories.Count > 0;
    public bool HasConfigBoundRegistrations =>
        DiscoveryResult.Options.Count > 0 ||
        DiscoveryResult.HttpClients.Count > 0;
    public bool HasHttpClientRegistrations =>
        DiscoveryResult.HttpClients.Count > 0;
    public bool HasProviderRegistrations => EmittedProviders.Count > 0;
}
