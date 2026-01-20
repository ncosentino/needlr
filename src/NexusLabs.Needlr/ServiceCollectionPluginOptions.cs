using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr;

public sealed record ServiceCollectionPluginOptions(
    IServiceCollection Services,
    IConfiguration Config,
    IReadOnlyList<Assembly> Assemblies,
    IPluginFactory PluginFactory);