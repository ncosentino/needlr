using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr;

/// <summary>
/// Options provided to <see cref="IServiceCollectionPlugin"/> implementations during configuration.
/// Contains the service collection, configuration, discovered assemblies, and plugin factory.
/// </summary>
/// <param name="Services">The service collection for registering dependencies.</param>
/// <param name="Config">The application configuration.</param>
/// <param name="Assemblies">
/// The candidate assemblies selected by Needlr. Source-generated discovery includes every
/// generated TypeRegistry participant, including empty registries, when assembly metadata is available.
/// </param>
/// <param name="PluginFactory">Factory for creating additional plugin instances.</param>
public sealed record ServiceCollectionPluginOptions(
    IServiceCollection Services,
    IConfiguration Config,
    IReadOnlyList<Assembly> Assemblies,
    IPluginFactory PluginFactory);