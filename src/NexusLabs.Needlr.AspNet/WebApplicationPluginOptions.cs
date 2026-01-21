using Microsoft.AspNetCore.Builder;

using System.Reflection;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Options provided to <see cref="IWebApplicationPlugin"/> implementations during configuration.
/// Contains the built web application, discovered assemblies, and plugin factory.
/// </summary>
/// <param name="WebApplication">The built web application to configure.</param>
/// <param name="Assemblies">The list of assemblies discovered by Needlr.</param>
/// <param name="PluginFactory">Factory for creating additional plugin instances.</param>
public sealed record WebApplicationPluginOptions(
    WebApplication WebApplication,
    IReadOnlyList<Assembly> Assemblies,
    IPluginFactory PluginFactory);
