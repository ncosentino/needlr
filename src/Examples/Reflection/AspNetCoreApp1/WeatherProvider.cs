
/// <summary>
/// Here is an example service that will be used by the minimal API in the 
/// Carter module. You do not need to add any attributes to this class, as it 
/// will be automatically registered by the Needlr framework.
/// </summary>
/// <param name="_config">
/// An <see cref="IConfiguration"/> provided by the dependency injection framework.
/// </param>
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