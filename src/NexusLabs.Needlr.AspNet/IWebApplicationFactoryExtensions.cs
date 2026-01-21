using Microsoft.AspNetCore.Builder;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Extension methods for <see cref="IWebApplicationFactory"/> providing convenient overloads for creating web applications.
/// </summary>
public static class IWebApplicationFactoryExtensions
{
    /// <summary>
    /// Creates a web application using the specified options with default configuration.
    /// </summary>
    /// <param name="factory">The web application factory.</param>
    /// <param name="options">The options for creating the web application.</param>
    /// <returns>A configured <see cref="WebApplication"/>.</returns>
    public static WebApplication Create(
       this IWebApplicationFactory factory,
       CreateWebApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        
        static void EmptyConfig(WebApplicationBuilder _, CreateWebApplicationOptions __) { }
        return factory.Create(options, EmptyConfig);
    }

    /// <summary>
    /// Creates a web application using the specified options and a configuration callback.
    /// </summary>
    /// <param name="factory">The web application factory.</param>
    /// <param name="options">The options for creating the web application.</param>
    /// <param name="configureCallback">Optional callback to configure the web application builder.</param>
    /// <returns>A configured <see cref="WebApplication"/>.</returns>
    public static WebApplication Create(
        this IWebApplicationFactory factory,
        CreateWebApplicationOptions options,
        Action<WebApplicationBuilder, CreateWebApplicationOptions>? configureCallback)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);

        WebApplicationBuilder Factory()
        {
            var webApplicationBuilder = WebApplication.CreateBuilder(options.Options);
            configureCallback?.Invoke(webApplicationBuilder, options);
            return webApplicationBuilder;
        }

        return factory.Create(options, Factory);
    }
}
