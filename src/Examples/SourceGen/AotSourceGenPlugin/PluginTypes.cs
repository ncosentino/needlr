using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr;
using NexusLabs.Needlr.AspNet;

namespace AotSourceGenPlugin;

public interface IWeatherProvider
{
    string GetForecast();
}

public sealed class WeatherProvider : IWeatherProvider
{
    public string GetForecast() => "Sunny with AOT";
}

public interface ITimeProvider
{
    DateTimeOffset GetNow();
}

public sealed class TimeProvider : ITimeProvider
{
    public DateTimeOffset GetNow() => DateTimeOffset.UtcNow;
}

[DoNotAutoRegister]
public interface IManualService
{
    string Echo(string value);
}

public sealed class ManualService : IManualService
{
    public string Echo(string value) => $"manual:{value}";
}

public sealed class DecoratedWeatherProvider(IWeatherProvider inner) : IWeatherProvider
{
    public string GetForecast() => $"[decorated] {inner.GetForecast()}";
}

// Manual registration via IServiceCollectionPlugin
public sealed class ManualRegistrationPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<IManualService, ManualService>();
    }
}

// Application-level plugin (web endpoints)
public sealed class WeatherPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options)
    {
        options.WebApplication.MapGet("/plugin-weather", static ([FromServices] IWeatherProvider weather) => Results.Text(weather.GetForecast()));
        options.WebApplication.MapGet("/plugin-time", static ([FromServices] ITimeProvider time) => Results.Text(time.GetNow().ToString("O")));
        options.WebApplication.MapGet("/plugin-manual/{value}", static ([FromServices] IManualService manual, string value) => Results.Text(manual.Echo(value)));
    }
}

// Post-build plugin for runtime verification
public sealed class StartupPlugin : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        var manual = options.Provider.GetRequiredService<IManualService>();
        Console.WriteLine($"StartupPlugin manual={manual.Echo("hi")}");
    }
}
