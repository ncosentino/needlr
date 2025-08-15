using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Scrutor;

// Needlr setup with Scrutor type registrar: build the provider and get our service
var serviceProvider = new Syringe()
    .UsingScrutorTypeRegistrar()
    .BuildServiceProvider();

Console.WriteLine("Needlr Scrutor Registration Example");
Console.WriteLine("===================================");

Console.WriteLine();
Console.WriteLine("Checking service provider registrations...");
Console.WriteLine(
    $"serviceProvider.GetService<MyScrutorPlugin>():    {serviceProvider.GetService<MyScrutorPlugin>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<IMyService>():         {serviceProvider.GetService<IMyService>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<MyService>():          {serviceProvider.GetService<MyService>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<MyDecorator>():        {serviceProvider.GetService<MyDecorator>() is not null}");
Console.WriteLine(
    $"serviceProvider.GetService<IConfiguration>():     {serviceProvider.GetService<IConfiguration>() is not null}");

Console.WriteLine();
Console.WriteLine("Calling service...");
serviceProvider.GetRequiredService<IMyService>().DoSomething();