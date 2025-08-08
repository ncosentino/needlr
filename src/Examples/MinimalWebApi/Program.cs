using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;

var webApplication = new Syringe().BuildWebApplication();
await webApplication.RunAsync();

internal sealed class WeatherPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options)
    {
        options.WebApplication.MapGet("/weather", (WeatherProvider weatherProvider) =>
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