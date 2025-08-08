using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Scrutor;

var webApplication = new Syringe()
    .UsingScrutorTypeRegistrar()
    .BuildWebApplication();
await webApplication.RunAsync();

internal sealed class WeatherPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options)
    {
        options.WebApplication.MapGet("/weather", (IConfiguration config) =>
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