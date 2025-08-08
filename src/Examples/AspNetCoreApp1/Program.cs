using Carter;

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
    candidateAssemblies);
var webApplication = webApplicationFactory.Create(options);
await webApplication.RunAsync();

internal sealed class WeatherCarterModule : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/weather", () =>
        {
            return Results.Ok(new
            {
                TemperatureC = 25,
                Summary = "Warm"
            });
        });
    }
}