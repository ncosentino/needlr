using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Scrutor;
using NexusLabs.Needlr.Injection.TypeFilterers;

var assemblyProvider = new AssembyProviderBuilder()
    .MatchingAssemblies(x => 
        x.Contains("NexusLabs", StringComparison.OrdinalIgnoreCase) ||
        x.Contains("AspNetCoreApp1", StringComparison.OrdinalIgnoreCase))
    .UseLibTestEntrySorting()
    .Build();
ScrutorTypeRegistrar typeRegistrar = new();
DefaultTypeFilterer typeFilterer = new();
ServiceCollectionPopulator serviceCollectionPopulator = new(
    typeRegistrar, 
    typeFilterer);
ServiceProviderBuilder serviceProviderBuilder = new(
    serviceCollectionPopulator,
    assemblyProvider,
    additionalAssemblies: []);
WebApplicationFactory webApplicationFactory = new(
    serviceProviderBuilder,
    serviceCollectionPopulator);
var options = CreateWebApplicationOptions.Default
    .UsingCliArgs(args)
    .UsingApplicationName("Dev Leader Weather App")
    .UsingStartupConsoleLogger();
var webApplication = webApplicationFactory.Create(
    options);
await webApplication.RunAsync();
