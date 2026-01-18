using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

// minimal Needler setup: build the provider and get our service
var serviceProvider = new Syringe()
    .UsingReflection()
    .BuildServiceProvider();

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