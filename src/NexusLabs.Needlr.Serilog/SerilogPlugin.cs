using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog;

namespace NexusLabs.Needlr.Serilog;

/// <summary>
/// Auto-discovered plugin that wires Serilog as the logging provider using
/// configuration from <c>appsettings.json</c> (or any <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>
/// source registered with Needlr).
/// </summary>
/// <remarks>
/// <para>
/// This plugin provides a zero-ceremony default: reference the
/// <c>NexusLabs.Needlr.Serilog</c> package and add a <c>"Serilog"</c> section
/// to <c>appsettings.json</c>. The plugin is auto-discovered and configures
/// <see cref="ILogger{TCategoryName}"/> resolution via DI.
/// </para>
/// <para>
/// The Serilog logger is owned by the DI container — sinks flush automatically
/// on container disposal. The static <see cref="Log.Logger"/> is not set — use
/// <see cref="ILogger{TCategoryName}"/> injection instead, or use
/// <see cref="NeedlrSerilogBootstrapper"/> for apps that need the static logger
/// and two-stage bootstrap lifecycle.
/// </para>
/// </remarks>
public sealed class SerilogPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(options.Config)
            .CreateLogger();

        options.Services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(logger, dispose: true);
        });
    }
}
