using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;

// Minimal setup with an additional registration. This is an approach I like to do
// when I write tests and I can override some of the existing services that
// would otherwise come from the standard IServiceProvider for my application. I
// might add a mocked interface or similar.
var serviceProvider = new Syringe()
    .UsingPostPluginRegistrationCallback(services =>
    {
        services.AddSingleton<IMyService, MyService>();
    })
    .AddDecorator<IMyService, MyDecorator>()
    .BuildServiceProvider();

Console.WriteLine("Needlr Manual Registration Example");
Console.WriteLine("==================================");

Console.WriteLine();
Console.WriteLine("Checking service provider registrations...");
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