using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

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
    /// Wires the agent-run, chat-completion, and function-calling middleware layers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IAgentDiagnosticsAccessor"/> and <see cref="IToolMetricsAccessor"/> are
    /// auto-registered by <c>UsingAgentFramework()</c> — no separate DI call needed.
    /// </para>
    /// <para>
    /// After calling this, wrap agent execution in
    /// <see cref="IAgentDiagnosticsAccessor.BeginCapture"/> to capture diagnostics:
    /// </para>
    /// <code>
    /// using (diagnosticsAccessor.BeginCapture())
    /// {
    ///     await agent.RunAsync(prompt);
    ///     var diag = diagnosticsAccessor.LastRunDiagnostics;
    /// }
    /// </code>
    /// </remarks>
    public static AgentFrameworkSyringe UsingDiagnostics(
        this AgentFrameworkSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        // Wrap the chat client with the diagnostics middleware so per-completion
        // token usage and timing are captured.
        var result = syringe.Configure(opts =>
        {
            var existingFactory = opts.ChatClientFactory;
            opts.ChatClientFactory = sp =>
            {
                var innerClient = existingFactory?.Invoke(sp)
                    ?? sp.GetRequiredService<IChatClient>();

                return innerClient
                    .AsBuilder()
                    .Use(getResponseFunc: DiagnosticsChatClientMiddleware.HandleAsync,
                        getStreamingResponseFunc: null)
                    .Build();
            };
        });

        // Add the diagnostics plugin for agent-run and function-calling middleware.
        // The plugin resolves the concrete AgentDiagnosticsAccessor from DI at Configure time.
        return result with
        {
            Plugins = (result.Plugins ?? [])
                .Append(new AgentDiagnosticsPlugin(syringe.ServiceProvider))
                .ToList()
        };
    }
}
