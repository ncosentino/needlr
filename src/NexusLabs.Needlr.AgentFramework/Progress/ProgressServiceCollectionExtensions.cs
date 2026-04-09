using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Extension methods for registering progress sinks in DI.
/// </summary>
public static class ProgressServiceCollectionExtensions
{
    /// <summary>
    /// Registers a progress sink as a singleton. Sinks registered this way are
    /// used as defaults by <see cref="IProgressReporterFactory.Create(string)"/>.
    /// </summary>
    /// <typeparam name="TSink">The sink type to register.</typeparam>
    public static IServiceCollection AddProgressSink<TSink>(this IServiceCollection services)
        where TSink : class, IProgressSink
    {
        services.AddSingleton<IProgressSink, TSink>();
        return services;
    }

    /// <summary>
    /// Registers a progress sink instance. Sinks registered this way are
    /// used as defaults by <see cref="IProgressReporterFactory.Create(string)"/>.
    /// </summary>
    public static IServiceCollection AddProgressSink(this IServiceCollection services, IProgressSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        services.AddSingleton(sink);
        return services;
    }
}
