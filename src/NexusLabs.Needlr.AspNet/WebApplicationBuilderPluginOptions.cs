using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

using System.Reflection;

namespace NexusLabs.Needlr.AspNet;

public sealed record WebApplicationBuilderPluginOptions(
    WebApplicationBuilder Builder,
    IReadOnlyList<Assembly> Assemblies,
    ILogger Logger);
