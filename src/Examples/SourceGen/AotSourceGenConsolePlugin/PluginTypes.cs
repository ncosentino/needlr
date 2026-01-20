using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr;

using System.Reflection;

namespace AotSourceGenConsolePlugin;

public interface IConsoleWeatherProvider
{
    string GetForecast();
}

internal sealed class ConsoleWeatherProvider(IConfiguration config) : IConsoleWeatherProvider
{
    public string GetForecast()
    {
        var prefix = config["Weather:Prefix"] ?? "";
        return string.IsNullOrWhiteSpace(prefix)
            ? "Sunny with AOT"
            : $"{prefix}: Sunny with AOT";
    }
}

public interface IConsoleTimeProvider
{
    DateTimeOffset GetNow();
}

internal sealed class ConsoleTimeProvider : IConsoleTimeProvider
{
    public DateTimeOffset GetNow() => DateTimeOffset.UtcNow;
}

[DoNotAutoRegister]
public interface IConsoleManualService
{
    string Echo(string value);
}

internal sealed class ConsoleManualService : IConsoleManualService
{
    public string Echo(string value) => $"manual:{value}";
}

// Manual registration via IServiceCollectionPlugin (must run, even though IConsoleManualService is [DoNotAutoRegister]).
internal sealed class ConsoleManualRegistrationPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<IConsoleManualService, ConsoleManualService>();
    }
}

public interface IConsoleReport
{
    string BuildReport();
}

internal sealed class ConsoleReport(
    IConsoleWeatherProvider weather,
    IConsoleTimeProvider time,
    Lazy<IConsoleManualService> manual,
    IReadOnlyList<Assembly> assemblies,
    IConfiguration config) : IConsoleReport
{
    public string BuildReport()
    {
        var greeting = config["Greeting"] ?? "(no greeting configured)";
        return string.Join(Environment.NewLine, new[]
        {
            $"greeting={greeting}",
            $"weather={weather.GetForecast()}",
            $"time={time.GetNow():O}",
            $"manual={manual.Value.Echo("from-report")}",
            $"assemblies={assemblies.Count}"
        });
    }
}

[DoNotInject]
public sealed class NotInjectedService;

// Post-build plugin for runtime verification.
internal sealed class ConsolePostBuildPlugin : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        var manual = options.Provider.GetRequiredService<IConsoleManualService>();
        Console.WriteLine($"ConsolePostBuildPlugin manual={manual.Echo("hi")}");

        var report = options.Provider.GetRequiredService<IConsoleReport>();
        Console.WriteLine("ConsolePostBuildPlugin report:\n" + report.BuildReport());
    }
}
