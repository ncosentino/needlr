namespace MinimalWebApiSourceGen;

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
