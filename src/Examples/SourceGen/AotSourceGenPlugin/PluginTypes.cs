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

/// <summary>
/// Decorator using [DecoratorFor] attribute - automatically wired by Needlr.
/// Order = 1 means this is applied first (closest to the original WeatherProvider).
/// </summary>
[DecoratorFor<IWeatherProvider>(Order = 1)]
public sealed class LoggingWeatherDecorator(IWeatherProvider inner) : IWeatherProvider
{
    public string GetForecast()
    {
        Console.WriteLine("[LoggingWeatherDecorator] Fetching forecast...");
        return inner.GetForecast();
    }
}

/// <summary>
/// Second decorator using [DecoratorFor] attribute.
/// Order = 2 means this wraps LoggingWeatherDecorator.
/// Resolution chain: PrefixWeatherDecorator → LoggingWeatherDecorator → WeatherProvider
/// </summary>
[DecoratorFor<IWeatherProvider>(Order = 2)]
public sealed class PrefixWeatherDecorator(IWeatherProvider inner) : IWeatherProvider
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
