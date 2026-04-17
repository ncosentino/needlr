using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workflows.Budget;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

/// <summary>
/// Extension methods for wiring agent diagnostics into the <see cref="AgentFrameworkSyringe"/>.
/// </summary>
public static class DiagnosticsExtensions
{
    /// <summary>
    /// Enables agent-run diagnostics for every agent created by the factory.
    /// Wires the agent-run, chat-completion, and function-calling middleware layers,
    /// and emits <see cref="IAgentMetrics"/> counters/histograms for OpenTelemetry.
    /// Automatically includes token tracking via <c>UsingTokenTracking()</c>.
    /// </summary>
    public static AgentFrameworkSyringe UsingDiagnostics(
        this AgentFrameworkSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        syringe = syringe.UsingTokenTracking();

        var result = syringe.Configure(opts =>
        {
            var metrics = opts.ServiceProvider.GetRequiredService<IAgentMetrics>();
            var progressAccessor = opts.ServiceProvider.GetRequiredService<IProgressReporterAccessor>();
            var chatMiddleware = new DiagnosticsChatClientMiddleware(metrics, progressAccessor);

            // Register the real collector via the DI-managed holder — NOT a static field.
            var holder = opts.ServiceProvider.GetRequiredService<ChatCompletionCollectorHolder>();
            holder.SetCollector(chatMiddleware);

            var existingFactory = opts.ChatClientFactory;
            opts.ChatClientFactory = sp =>
            {
                var innerClient = existingFactory?.Invoke(sp)
                    ?? sp.GetRequiredService<IChatClient>();

                return innerClient
                    .AsBuilder()
                    .Use(getResponseFunc: chatMiddleware.HandleAsync,
                        getStreamingResponseFunc: null)
                    .Build();
            };
        });

        return result with
        {
            Plugins = (result.Plugins ?? [])
                .Append(new AgentDiagnosticsPlugin(syringe.ServiceProvider))
                .ToList()
        };
    }
}
