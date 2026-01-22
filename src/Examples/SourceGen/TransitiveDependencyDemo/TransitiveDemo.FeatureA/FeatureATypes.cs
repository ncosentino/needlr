using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr;

namespace TransitiveDemo.FeatureA;

/// <summary>
/// A core service registered by FeatureA's plugin.
/// Other features may depend on this service.
/// </summary>
public interface ICoreLogger
{
    void Log(string message);
}

internal sealed class CoreLogger : ICoreLogger
{
    private readonly List<string> _logs = [];

    public void Log(string message)
    {
        _logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] {message}");
        Console.WriteLine($"[CoreLogger] {message}");
    }

    public IReadOnlyList<string> GetLogs() => _logs;
}

/// <summary>
/// This plugin registers ICoreLogger.
/// 
/// CRITICAL: This plugin will ONLY run if FeatureA's assembly is loaded!
/// Without the automatic force-loading feature, if no code in the host
/// directly references types from FeatureA, this plugin would never execute.
/// </summary>
internal sealed class FeatureAPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        Console.WriteLine("[FeatureA] Plugin executing - registering ICoreLogger");
        options.Services.AddSingleton<ICoreLogger, CoreLogger>();
    }
}
