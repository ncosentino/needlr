using Microsoft.Extensions.Configuration;

using System.Reflection;

namespace NexusLabs.Needlr;

public sealed record PostBuildServiceCollectionPluginOptions(
    IServiceProvider Provider,
    IConfiguration Config,
    IReadOnlyList<Assembly> Assemblies);
