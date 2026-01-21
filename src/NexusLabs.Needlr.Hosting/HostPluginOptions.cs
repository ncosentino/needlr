using Microsoft.Extensions.Hosting;

using System.Reflection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Options passed to <see cref="IHostPlugin.Configure"/>.
/// </summary>
public sealed record HostPluginOptions(
    IHost Host,
    IReadOnlyList<Assembly> Assemblies,
    IPluginFactory PluginFactory);
