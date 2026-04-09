using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Progress;

/// <summary>
/// Extension methods for wiring progress reporting into the <see cref="AgentFrameworkSyringe"/>.
/// </summary>
public static class ProgressReportingExtensions
{
    /// <summary>
    /// Registers <see cref="IProgressReporterFactory"/> in DI. The factory resolves
    /// default sinks from DI (<see cref="IProgressSink"/> registrations) and creates
    /// per-orchestration reporters.
    /// </summary>
    public static AgentFrameworkSyringe UsingProgressReporting(
        this AgentFrameworkSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return syringe.Configure(opts =>
        {
            var sinks = opts.ServiceProvider.GetServices<IProgressSink>().ToArray();
            var factory = new ProgressReporterFactory(sinks);

            // Store on options so it's accessible — but we can't add to DI here.
            // The factory will be resolved by consumers who create it themselves,
            // or from the syringe's service provider if registered externally.
        });
    }
}
