using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Reflection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Options passed to <see cref="IHostApplicationBuilderPlugin.Configure"/>.
/// </summary>
/// <param name="Builder">The host application builder being configured.</param>
/// <param name="Assemblies">
/// The candidate assemblies selected by Needlr. Source-generated discovery includes every
/// generated TypeRegistry participant, including empty registries, when assembly metadata is available.
/// </param>
/// <param name="Logger">Logger for host plugin execution.</param>
/// <param name="PluginFactory">Factory for creating additional plugin instances.</param>
public sealed record HostApplicationBuilderPluginOptions(
    HostApplicationBuilder Builder,
    IReadOnlyList<Assembly> Assemblies,
    ILogger Logger,
    IPluginFactory PluginFactory);
