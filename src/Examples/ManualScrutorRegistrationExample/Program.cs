using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Scrutor;

// Needlr setup with Scrutor type registrar: build the provider and get our service
var serviceProvider = new Syringe()
    .UsingScrutorTypeRegistrar()
    .BuildServiceProvider();

serviceProvider.GetRequiredService<IMyService>().DoSomething();