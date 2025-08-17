using Microsoft.AspNetCore.Builder;

namespace NexusLabs.Needlr.AspNet;

public static class IWebApplicationFactoryExtensions
{
    public static WebApplication Create(
       this IWebApplicationFactory factory,
       CreateWebApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        
        static void EmptyConfig(WebApplicationBuilder _, CreateWebApplicationOptions __) { }
        return factory.Create(options, EmptyConfig);
    }

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
