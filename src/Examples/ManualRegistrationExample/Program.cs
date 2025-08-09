using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;

// minimal Needler setup: build the provider and get our service
var serviceProvider = new Syringe().BuildServiceProvider();
serviceProvider.GetRequiredService<IMyService>().DoSomething();