using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Scrutor;
using NexusLabs.Needlr.Injection.TypeFilterers;

using var loggerFactory = LoggerFactory
    .Create(builder => builder
    .AddConsole()
    .SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger("AspNetCoreApp1.Startup");

var assemblyProvider = new AssembyProviderBuilder()
    .MatchingAssemblies(x => 
        x.Contains("NexusLabs", StringComparison.OrdinalIgnoreCase) ||
        x.Contains("AspNetCoreApp1", StringComparison.OrdinalIgnoreCase))
    .UseLibTestEntrySorting()
    .Build();
var candidateAssemblies = assemblyProvider.GetCandidateAssemblies();
ScrutorTypeRegistrar typeRegistrar = new();
var typeFilterer = new DefaultTypeFilterer();
ServiceCollectionPopulator serviceCollectionPopulator = new(
    candidateAssemblies, 
    typeRegistrar, 
    typeFilterer);
ServiceProviderBuilder serviceProviderBuilder = new(
    serviceCollectionPopulator,
    assemblyProvider,
    candidateAssemblies);
WebApplicationFactory webApplicationFactory = new(
    serviceProviderBuilder,
    serviceCollectionPopulator);
CreateWebApplicationOptions options = new(
    new WebApplicationOptions()
    {
        Args = args,
        ApplicationName = "AspNetCoreApp1",
    },
    candidateAssemblies,
    logger);
var webApplication = webApplicationFactory.Create(options);
await webApplication.RunAsync();
