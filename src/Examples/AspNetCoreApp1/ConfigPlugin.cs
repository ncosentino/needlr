using NexusLabs.Needlr.AspNet;

/// <summary>
/// A plugin for configuring the <see cref="IConfigurationBuilder"/> on 
/// the <see cref="WebApplicationBuilder"/>. You do not need to add any
/// attributes to this class, as it implements the 
/// <see cref="IWebApplicationBuilderPlugin"/> interface, which will be
/// automatically registered and invoked by the Needlr framework.
/// </summary>
/// <remarks>
/// If the entry point does not pass a config in to build the web app, we
/// end up overriding it on the <see cref="WebApplicationBuilder"/> but
/// not on the <see cref="WebApplicationBuilderPluginOptions"/>. If you need
/// configuration done from the very start, pass it into the Needlr framework.
/// </remarks>
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
            configurationBuilder.AddJsonFile(
                $"appsettings.json", 
                optional: true,
                reloadOnChange: true);
        }

        configurationBuilder.AddJsonFile(
            $"appsettings.{webApplicationBuilder.Environment.EnvironmentName}.json",
            optional: true,
            reloadOnChange: true);

        // NOTE: you can uncomment this and prove that this plugin will override the
        // default configuration that is set in appsettings.json and appsettings.{env}.json
        //configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        //{
        //    ["Weather:TemperatureCelsius"] = "1337",
        //    ["Weather:Summary"] = "This is from the in memory provider"
        //});
    }
}