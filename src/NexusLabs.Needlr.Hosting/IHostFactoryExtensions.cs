using Microsoft.Extensions.Hosting;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Extension methods for <see cref="IHostFactory"/>.
/// </summary>
public static class IHostFactoryExtensions
{
    /// <summary>
    /// Creates an <see cref="IHost"/> using the specified options.
    /// </summary>
    /// <param name="factory">The host factory.</param>
    /// <param name="options">The options for creating the host.</param>
    /// <returns>The configured <see cref="IHost"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> or <paramref name="options"/> is null.</exception>
    public static IHost Create(
        this IHostFactory factory,
        CreateHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);

        static void EmptyConfig(HostApplicationBuilder _, CreateHostOptions __) { }
        return factory.Create(options, EmptyConfig);
    }

    /// <summary>
    /// Creates an <see cref="IHost"/> using the specified options and configuration callback.
    /// </summary>
    /// <param name="factory">The host factory.</param>
    /// <param name="options">The options for creating the host.</param>
    /// <param name="configureCallback">Optional callback to configure the <see cref="HostApplicationBuilder"/>.</param>
    /// <returns>The configured <see cref="IHost"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> or <paramref name="options"/> is null.</exception>
    public static IHost Create(
        this IHostFactory factory,
        CreateHostOptions options,
        Action<HostApplicationBuilder, CreateHostOptions>? configureCallback)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);

        HostApplicationBuilder Factory()
        {
            var hostApplicationBuilder = Host.CreateApplicationBuilder(options.Settings);
            configureCallback?.Invoke(hostApplicationBuilder, options);
            return hostApplicationBuilder;
        }

        return factory.Create(options, Factory);
    }
}
