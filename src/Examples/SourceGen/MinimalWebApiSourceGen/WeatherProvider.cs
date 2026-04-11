using Microsoft.Extensions.Options;

namespace MinimalWebApiSourceGen;

/// <summary>
/// Consumes <see cref="WeatherOptions"/> via the standard
/// <see cref="IOptions{TOptions}"/> pattern. The constructor will only
/// receive bound values when the Needlr source-generated options
/// registration has been invoked — which is the behavior that the web path
/// fix in
/// <see cref="NexusLabs.Needlr.AspNet.WebApplicationSyringe.BuildWebApplication"/>
/// now guarantees.
/// </summary>
internal sealed class WeatherProvider(
    IOptions<WeatherOptions> _options)
{
    public object GetWeather()
    {
        var weather = _options.Value;
        return new
        {
            TemperatureC = weather.TemperatureCelsius,
            Summary = weather.Summary,
            Source = $"[Options] source-gen binding -> WeatherOptions ('{nameof(weather.Summary)}' populated from appsettings Weather:Summary)",
        };
    }
}
