using MinimalWebApiSourceGen;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Generated;
using NexusLabs.Needlr.Injection;

// Build web application using source generation instead of reflection.
// This uses the compile-time generated TypeRegistry for AOT compatibility
// and better startup performance.
var webApplication = new Syringe().BuildWebApplication();
var webAppTask = webApplication.RunAsync();

var serviceProvider = webApplication.Services;
Console.WriteLine("MinimalWebApiSourceGen Example");
Console.WriteLine("==============================");
Console.WriteLine("This example uses source generation instead of reflection.");
Console.WriteLine();

Console.WriteLine("Checking service provider registrations...");
Console.WriteLine(
    $"serviceProvider.GetService<WeatherPlugin>():   {serviceProvider.GetService<WeatherPlugin>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<WeatherProvider>(): {serviceProvider.GetService<WeatherProvider>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<IConfiguration>():  {serviceProvider.GetService<IConfiguration>() is not null}");

await webAppTask;
