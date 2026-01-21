namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Defines a plugin that configures the <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder"/> before the application is built.
/// Implement this interface to add services, configure logging, or modify the builder during application startup.
/// </summary>
[DoNotAutoRegister]
[DoNotInject]
public interface IWebApplicationBuilderPlugin
{
    void Configure(WebApplicationBuilderPluginOptions options);
}
