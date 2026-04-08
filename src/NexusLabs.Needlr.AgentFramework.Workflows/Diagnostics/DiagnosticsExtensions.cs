using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

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
    /// </summary>
    public static AgentFrameworkSyringe UsingDiagnostics(
        this AgentFrameworkSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        var result = syringe.Configure(opts =>
        {
            var metrics = opts.ServiceProvider.GetRequiredService<IAgentMetrics>();
            var chatMiddleware = new DiagnosticsChatClientMiddleware(metrics);

            // Store the middleware instance so PipelineRunExtensions can drain
            // per-LLM-call completions at turn boundaries.
            ChatMiddlewareHolder.Instance = chatMiddleware;

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

/// <summary>
/// Holds the <see cref="DiagnosticsChatClientMiddleware"/> instance so
/// <see cref="PipelineRunExtensions"/> can drain completions at turn boundaries.
/// </summary>
internal static class ChatMiddlewareHolder
{
    internal static DiagnosticsChatClientMiddleware? Instance { get; set; }
}
