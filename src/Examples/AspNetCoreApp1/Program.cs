using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Scrutor;

var webApplication = new Syringe()
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
    .BuildWebApplication();

var webAppTask = webApplication.RunAsync();

var serviceProvider = webApplication.Services;
Console.WriteLine("AspNetCoreApp1 Example");
Console.WriteLine("======================");

Console.WriteLine();
Console.WriteLine("Checking service provider registrations...");
Console.WriteLine(
    $"serviceProvider.GetService<ConfigPlugin>():                        {serviceProvider.GetService<ConfigPlugin>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<GeneralWebApplicationBuilderPlugin>():  {serviceProvider.GetService<GeneralWebApplicationBuilderPlugin>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<WeatherCarterModule>():                 {serviceProvider.GetService<WeatherCarterModule>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<WeatherProvider>():                     {serviceProvider.GetService<WeatherProvider>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<IConfiguration>():                      {serviceProvider.GetService<IConfiguration>() is not null}");

await webAppTask;
