using NexusLabs.Needlr.AspNet;

internal sealed class ConfigPlugin : IWebApplicationBuilderPlugin
{
    public void Configure(WebApplicationBuilderPluginOptions options)
    {
        var webApplicationBuilder = options.Builder;
        var configurationManager = webApplicationBuilder
            .Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddEnvironmentVariables();

        // only add base configuration files if not in test environment
        if (!webApplicationBuilder.Environment.IsEnvironment("Test"))
        {
            configurationManager.AddJsonFile($"appsettings.json", optional: true, reloadOnChange: true);
        }

        configurationManager.AddJsonFile($"appsettings.{webApplicationBuilder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
    }
}