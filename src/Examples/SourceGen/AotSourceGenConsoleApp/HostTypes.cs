using AotSourceGenConsolePlugin;

using Microsoft.Extensions.Configuration;

using NexusLabs.Needlr;

namespace AotSourceGenConsoleApp;

public interface IHostGreeter
{
    string GetGreeting();
}

public sealed class HostGreeter(IConfiguration config) : IHostGreeter
{
    public string GetGreeting() => config["Greeting"] ?? "(no greeting configured)";
}

/// <summary>
/// Decorator using [DecoratorFor] attribute - Order 1 is applied first (closest to original).
/// </summary>
[DecoratorFor<IConsoleWeatherProvider>(Order = 1)]
public sealed class HostLoggingWeatherDecorator(IConsoleWeatherProvider inner) : IConsoleWeatherProvider
{
    public string GetForecast()
    {
        Console.WriteLine("[Host:Logging] Getting weather forecast...");
        return inner.GetForecast();
    }
}

/// <summary>
/// Decorator using [DecoratorFor] attribute - Order 2 wraps the Order 1 decorator.
/// Resolution: HostPrefixWeatherDecorator → HostLoggingWeatherDecorator → ConsoleWeatherProvider
/// </summary>
[DecoratorFor<IConsoleWeatherProvider>(Order = 2)]
public sealed class HostPrefixWeatherDecorator(
    IConsoleWeatherProvider inner,
    IHostGreeter greeter) : IConsoleWeatherProvider
{
    public string GetForecast() => $"{greeter.GetGreeting()} | [host-decorated] {inner.GetForecast()}";
}
