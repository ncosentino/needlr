using Microsoft.AspNetCore.Builder;

using System.Reflection;

namespace NexusLabs.Needlr.AspNet;

public sealed record WebApplicationPluginOptions(
    WebApplication WebApplication,
    IReadOnlyList<Assembly> Assemblies,
    IPluginFactory PluginFactory);
