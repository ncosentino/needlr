using Carter;

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

internal sealed class WeatherProvider(
    IConfiguration _config)
{
    public object GetWeather()
    {
        var weatherConfig = _config.GetSection("Weather");
        return new
        {
            TemperatureC = weatherConfig.GetValue<double>("TemperatureCelsius"),
            Summary = weatherConfig.GetValue<string>("Summary"),
        };
    }
}