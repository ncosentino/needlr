using Microsoft.AspNetCore.Builder;

namespace NexusLabs.Needlr.AspNet;
public interface IWebApplicationFactory
{
    WebApplication Create(
        CreateWebApplicationOptions options, 
        Func<WebApplicationBuilder> createWebApplicationBuilderCallback);
}