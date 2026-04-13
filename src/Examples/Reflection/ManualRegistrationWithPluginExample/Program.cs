using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAdditionalAssemblies([typeof(NexusLabs.Needlr.Serilog.SerilogPlugin).Assembly])
    .BuildServiceProvider(config);

// Note: UsingAdditionalAssemblies is needed in reflection mode because the default
// assembly scanner doesn't load NexusLabs.Needlr.Serilog automatically. In source-gen
// mode (UsingSourceGen), the plugin is discovered via [ModuleInitializer] bootstrap
// and no additional assembly configuration is needed.

Console.WriteLine("Needlr Manual Registration With Plugin Example");
Console.WriteLine("==============================================");

Console.WriteLine();
Console.WriteLine("Checking service provider registrations...");
Console.WriteLine(
    $"serviceProvider.GetService<MyPlugin>():       {serviceProvider.GetService<MyPlugin>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<IMyService>():     {serviceProvider.GetService<IMyService>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<MyService>():      {serviceProvider.GetService<MyService>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<MyDecorator>():    {serviceProvider.GetService<MyDecorator>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<IConfiguration>(): {serviceProvider.GetService<IConfiguration>() is not null}");

Console.WriteLine();
Console.WriteLine("Calling service...");
serviceProvider.GetRequiredService<IMyService>().DoSomething();

Console.WriteLine();
Console.WriteLine("Serilog Plugin Demo");
Console.WriteLine("===================");
Console.WriteLine("SerilogPlugin auto-discovered from NexusLabs.Needlr.Serilog assembly.");
Console.WriteLine("ILogger<T> configured from appsettings.json Serilog section:");
Console.WriteLine();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Structured log via {Source} — zero manual Serilog setup", "ILogger<Program>");
logger.LogWarning("Warning with {Key} = {Value}", "DemoProperty", 42);