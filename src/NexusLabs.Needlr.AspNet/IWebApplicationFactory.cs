using Microsoft.AspNetCore.Builder;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Factory interface for creating configured <see cref="WebApplication"/> instances.
/// Handles the full application lifecycle including service registration, plugin execution, and configuration.
/// </summary>
public interface IWebApplicationFactory
{
    WebApplication Create(
        CreateWebApplicationOptions options, 
        Func<WebApplicationBuilder> createWebApplicationBuilderCallback);
}