using Microsoft.AspNetCore.Builder;

using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Extension methods for configuring <see cref="Syringe"/> instances with ASP.NET Core functionality.
/// </summary>
public static class SyringeAspNetExtensions
{
    /// <summary>
    /// Transitions the syringe to web application mode, enabling web-specific configuration.
    /// </summary>
    /// <param name="syringe">The syringe to transition.</param>
    /// <returns>A new web application syringe instance.</returns>
    public static WebApplicationSyringe ForWebApplication(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return new WebApplicationSyringe(syringe);
    }

    /// <summary>
    /// Builds a web application with the configured settings using the default WebApplicationFactory.
    /// </summary>
    /// <param name="syringe">The syringe to build from.</param>
    /// <returns>The configured <see cref="WebApplication"/>.</returns>
    public static WebApplication BuildWebApplication(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.ForWebApplication().BuildWebApplication();
    }
}