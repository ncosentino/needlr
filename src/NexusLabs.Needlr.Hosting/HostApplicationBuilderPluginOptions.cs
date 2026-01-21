using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Reflection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Options passed to <see cref="IHostApplicationBuilderPlugin.Configure"/>.
/// </summary>
public sealed record HostApplicationBuilderPluginOptions(
    HostApplicationBuilder Builder,
    IReadOnlyList<Assembly> Assemblies,
    ILogger Logger,
    IPluginFactory PluginFactory);
