using System.Collections.Generic;
using System.Linq;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Normalizes discovery results into the exact inputs consumed by registration emitters.
/// </summary>
internal static class GeneratedRegistrationPlanBuilder
{
    internal static GeneratedRegistrationPlan Create(
        string assemblyName,
        bool isAotProject,
        bool registersServiceCatalog,
        IReadOnlyList<string> referencedRegistryAssemblies,
        DiscoveryResult discoveryResult)
    {
        var emittedFactories = discoveryResult.Factories
            .Where(factory =>
                factory.GenerateFunc ||
                factory.GenerateInterface)
            .ToArray();
        var emittedProviders = discoveryResult.Providers
            .Where(provider =>
                provider.IsInterface ||
                provider.IsPartial)
            .ToArray();

        return new GeneratedRegistrationPlan(
            assemblyName,
            isAotProject,
            registersServiceCatalog,
            referencedRegistryAssemblies,
            emittedFactories,
            emittedProviders,
            discoveryResult);
    }
}
