namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Defines a plugin that configures the <see cref="Microsoft.AspNetCore.Builder.WebApplication"/> after it has been built.
/// Implement this interface to add middleware, configure endpoints, or perform other post-build configuration.
/// </summary>
[DoNotAutoRegister]
[DoNotInject]
public interface IWebApplicationPlugin
{
    void Configure(WebApplicationPluginOptions options);
}
