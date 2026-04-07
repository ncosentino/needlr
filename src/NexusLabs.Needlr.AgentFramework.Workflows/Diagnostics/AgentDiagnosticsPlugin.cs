using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

/// <summary>
/// <see cref="IAIAgentBuilderPlugin"/> that wires the diagnostics middleware layers
/// into every agent created by the factory:
/// <list type="number">
///   <item>Agent-run middleware (outermost) — captures total duration, message counts, success/failure.</item>
///   <item>Function-calling middleware — captures per-tool timing and custom metrics.</item>
/// </list>
/// The chat-completion middleware is wired separately on the <see cref="Microsoft.Extensions.AI.IChatClient"/>
/// via <see cref="DiagnosticsExtensions.UsingDiagnostics"/>.
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

        // Resolve the concrete accessor from DI to access the internal Set() method.
        var accessor = (AgentDiagnosticsAccessor)_serviceProvider
            .GetRequiredService<IAgentDiagnosticsAccessor>();

        var runMiddleware = new DiagnosticsAgentRunMiddleware("Agent", accessor);
        builder.Use(
            runFunc: runMiddleware.HandleAsync,
            // Streaming passes through without diagnostics capture.
            runStreamingFunc: (messages, session, options, innerAgent, ct) =>
                innerAgent.RunStreamingAsync(messages, session, options, ct));

        DiagnosticsFunctionCallingMiddleware.Wire(builder);
    }
}
