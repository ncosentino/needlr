using NexusLabs.Needlr.AspNet;

/// <summary>
/// A plugin for configuring the <see cref="IConfigurationBuilder"/> on 
/// the <see cref="WebApplicationBuilder"/>. You do not need to add any
/// attributes to this class, as it implements the 
/// <see cref="IWebApplicationBuilderPlugin"/> interface, which will be
/// automatically registered and invoked by the Needlr framework.
/// </summary>
internal sealed class ConfigPlugin : IWebApplicationBuilderPlugin
{
    public void Configure(WebApplicationBuilderPluginOptions options)
    {
        var webApplicationBuilder = options.Builder;
        var configurationBuilder = webApplicationBuilder
            .Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddEnvironmentVariables();

        // only add base configuration files if not in test environment
        if (!webApplicationBuilder.Environment.IsEnvironment("Test"))
        {
            configurationBuilder.AddJsonFile($"appsettings.json", optional: true, reloadOnChange: true);
        }

        configurationBuilder.AddJsonFile($"appsettings.{webApplicationBuilder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
    }
}