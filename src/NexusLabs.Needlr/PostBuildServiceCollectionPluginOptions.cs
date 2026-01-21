using Microsoft.Extensions.Configuration;

using System.Reflection;

namespace NexusLabs.Needlr;

/// <summary>
/// Options provided to <see cref="IPostBuildServiceCollectionPlugin"/> implementations during configuration.
/// Contains the built service provider, configuration, discovered assemblies, and plugin factory.
/// </summary>
/// <param name="Provider">The built service provider for resolving dependencies.</param>
/// <param name="Config">The application configuration.</param>
/// <param name="Assemblies">The list of assemblies discovered by Needlr.</param>
/// <param name="PluginFactory">Factory for creating additional plugin instances.</param>
public sealed record PostBuildServiceCollectionPluginOptions(
    IServiceProvider Provider,
    IConfiguration Config,
    IReadOnlyList<Assembly> Assemblies,
    IPluginFactory PluginFactory);
