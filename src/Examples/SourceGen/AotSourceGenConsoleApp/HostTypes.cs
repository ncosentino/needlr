using AotSourceGenConsolePlugin;

using Microsoft.Extensions.Configuration;

namespace AotSourceGenConsoleApp;

public interface IHostGreeter
{
    string GetGreeting();
}

public sealed class HostGreeter(IConfiguration config) : IHostGreeter
{
    public string GetGreeting() => config["Greeting"] ?? "(no greeting configured)";
}

public sealed class DecoratedConsoleWeatherProvider(
    IConsoleWeatherProvider inner,
    IHostGreeter greeter) : IConsoleWeatherProvider
{
    public string GetForecast() => $"{greeter.GetGreeting()} | [decorated] {inner.GetForecast()}";
}
