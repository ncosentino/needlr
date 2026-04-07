using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

/// <summary>
/// <see cref="IAIAgentBuilderPlugin"/> that wires the diagnostics middleware layers
/// into every agent created by the factory. Emits <see cref="IAgentMetrics"/>
/// counters and histograms for runs, tool calls, and chat completions.
/// </summary>
internal sealed class AgentDiagnosticsPlugin : IAIAgentBuilderPlugin
{
    private readonly IServiceProvider _serviceProvider;

    internal AgentDiagnosticsPlugin(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public void Configure(AIAgentBuilderPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = options.AgentBuilder;

        var writer = _serviceProvider.GetRequiredService<IAgentDiagnosticsWriter>();
        var metrics = _serviceProvider.GetRequiredService<IAgentMetrics>();

        var runMiddleware = new DiagnosticsAgentRunMiddleware("Agent", writer, metrics);
        builder.Use(
            runFunc: runMiddleware.HandleAsync,
            runStreamingFunc: (messages, session, runOptions, innerAgent, ct) =>
                innerAgent.RunStreamingAsync(messages, session, runOptions, ct));

        DiagnosticsFunctionCallingMiddleware.Wire(builder, metrics);
    }
}
