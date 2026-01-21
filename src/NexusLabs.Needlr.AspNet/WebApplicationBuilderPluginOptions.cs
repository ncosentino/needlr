using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

using System.Reflection;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Options provided to <see cref="IWebApplicationBuilderPlugin"/> implementations during configuration.
/// Contains the web application builder, discovered assemblies, logger, and plugin factory.
/// </summary>
/// <param name="Builder">The web application builder being configured.</param>
/// <param name="Assemblies">The list of assemblies discovered by Needlr.</param>
/// <param name="Logger">Logger for diagnostic output during plugin execution.</param>
/// <param name="PluginFactory">Factory for creating additional plugin instances.</param>
public sealed record WebApplicationBuilderPluginOptions(
    WebApplicationBuilder Builder,
    IReadOnlyList<Assembly> Assemblies,
    ILogger Logger,
    IPluginFactory PluginFactory);
