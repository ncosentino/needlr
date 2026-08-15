using Microsoft.Extensions.Hosting;

using System.Reflection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Options passed to <see cref="IHostPlugin.Configure"/>.
/// </summary>
/// <param name="Host">The built host being configured.</param>
/// <param name="Assemblies">
/// The candidate assemblies selected by Needlr. Source-generated discovery includes every
/// generated TypeRegistry participant, including empty registries, when assembly metadata is available.
/// </param>
/// <param name="PluginFactory">Factory for creating additional plugin instances.</param>
public sealed record HostPluginOptions(
    IHost Host,
    IReadOnlyList<Assembly> Assemblies,
    IPluginFactory PluginFactory);
