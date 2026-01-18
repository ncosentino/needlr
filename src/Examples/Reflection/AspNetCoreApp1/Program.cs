using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.Scrutor;

var webApplication = new Syringe()
    .UsingReflection()
    .UsingScrutorTypeRegistrar()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x =>
            x.Contains("NexusLabs", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("AspNetCoreApp1", StringComparison.OrdinalIgnoreCase))
        .UseLibTestEntrySorting()
        .Build())
    .UsingAdditionalAssemblies(additionalAssemblies: [])
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions
        .Default
        .UsingStartupConsoleLogger())
    .UsingConfigurationCallback((webApplicationBuilder, options) =>
    {
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

        // NOTE: you can uncomment this and prove that this code will override the
        // default configuration that is set in appsettings.json and appsettings.{env}.json
        //configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        //{
        //    ["Weather:TemperatureCelsius"] = "1337",
        //    ["Weather:Summary"] = "This is from the in memory provider"
        //});
    })
    .BuildWebApplication();

var webAppTask = webApplication.RunAsync();

var serviceProvider = webApplication.Services;
Console.WriteLine("AspNetCoreApp1 Example");
Console.WriteLine("======================");

Console.WriteLine();
Console.WriteLine("Checking service provider registrations...");
Console.WriteLine(
    $"serviceProvider.GetService<GeneralWebApplicationBuilderPlugin>():  {serviceProvider.GetService<GeneralWebApplicationBuilderPlugin>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<WeatherCarterModule>():                 {serviceProvider.GetService<WeatherCarterModule>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<WeatherProvider>():                     {serviceProvider.GetService<WeatherProvider>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<IConfiguration>():                      {serviceProvider.GetService<IConfiguration>() is not null}");

await webAppTask;
