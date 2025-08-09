using Carter;

/// <summary>
/// Here's an example of a Carter module that will be automatically registered
/// and invoked by the Needlr framework. You do not need to add any attributes,
/// and it can be left as 'internal' if you don't need it exposed beyond the assembly.
/// </summary>
internal sealed class WeatherCarterModule : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/weather", (WeatherProvider weatherProvider) =>
        {
            return Results.Ok(weatherProvider.GetWeather());
        });
    }
}
