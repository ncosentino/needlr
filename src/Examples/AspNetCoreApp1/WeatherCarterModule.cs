using Carter;

internal sealed class WeatherCarterModule : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/weather", (IConfiguration config) =>
        {
            var weatherConfig = config.GetSection("Weather");
            return Results.Ok(new
            {
                TemperatureC = weatherConfig.GetValue<double>("TemperatureCelsius"),
                Summary = weatherConfig.GetValue<string>("Summary"),
            });
        });
    }
}
