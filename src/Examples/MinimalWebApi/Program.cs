using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;

var webApplication = new Syringe().BuildWebApplication();

var webAppTask = webApplication.RunAsync();

var serviceProvider = webApplication.Services;
Console.WriteLine("MinimalWebApi Example");
Console.WriteLine("=====================");

Console.WriteLine();
Console.WriteLine("Checking service provider registrations...");
Console.WriteLine(
    $"serviceProvider.GetService<WeatherPlugin>():   {serviceProvider.GetService<WeatherPlugin>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<WeatherProvider>(): {serviceProvider.GetService<WeatherProvider>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<IConfiguration>():  {serviceProvider.GetService<IConfiguration>() is not null}");

await webAppTask;
