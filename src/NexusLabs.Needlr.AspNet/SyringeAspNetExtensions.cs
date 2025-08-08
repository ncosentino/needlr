using Microsoft.AspNetCore.Builder;
using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Extension methods for configuring <see cref="Syringe"/> instances with ASP.NET Core functionality.
/// </summary>
/// <example>
/// Complete web application setup:
/// <code>
/// var webApplication = new Syringe()
///     .UsingScrutorTypeRegistrar()
///     .UsingDefaultTypeFilterer()
///     .UsingAssemblyProvider(builder => builder
///         .MatchingAssemblies(x => x.Contains("MyApp"))
///         .Build())
///     .ForWebApplication()
///     .UsingOptions(() => CreateWebApplicationOptions.Default
///         .UsingCliArgs(args)
///         .UsingApplicationName("My Web App"))
///     .BuildWebApplication();
/// 
/// await webApplication.RunAsync();
/// </code>
/// 
/// Service provider only:
/// <code>
/// var serviceProvider = new Syringe()
///     .UsingScrutorTypeRegistrar()
///     .BuildServiceProvider();
/// </code>
/// </example>
public static class SyringeAspNetExtensions
{
    /// <summary>
    /// Transitions the syringe to web application mode, enabling web-specific configuration.
    /// </summary>
    /// <param name="syringe">The syringe to transition.</param>
    /// <returns>A new web application syringe instance.</returns>
    /// <example>
    /// <code>
    /// var webAppSyringe = new Syringe()
    ///     .UsingScrutorTypeRegistrar()
    ///     .UsingDefaultTypeFilterer()
    ///     .UsingAssemblyProvider(builder => builder
    ///         .MatchingAssemblies(x => x.Contains("MyApp"))
    ///         .Build())
    ///     .ForWebApplication(); // Transition to web application mode
    /// 
    /// // Now you can use web-specific methods
    /// var webApp = webAppSyringe
    ///     .UsingOptions(() => CreateWebApplicationOptions.Default.UsingCliArgs(args))
    ///     .BuildWebApplication();
    /// </code>
    /// </example>
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
    /// <example>
    /// <code>
    /// // Direct build without additional web configuration
    /// var webApplication = new Syringe()
    ///     .UsingScrutorTypeRegistrar()
    ///     .UsingDefaultTypeFilterer()
    ///     .BuildWebApplication();
    /// 
    /// await webApplication.RunAsync();
    /// </code>
    /// </example>
    public static WebApplication BuildWebApplication(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.ForWebApplication().BuildWebApplication();
    }
}