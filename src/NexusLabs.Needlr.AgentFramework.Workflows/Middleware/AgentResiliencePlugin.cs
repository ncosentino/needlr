using Microsoft.Agents.AI;

using NexusLabs.Needlr.AgentFramework;

using Polly;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Middleware;

/// <summary>
/// MAF agent-level middleware that wraps each agent run call in a
/// <see cref="ResiliencePipeline{TResult}"/> from Microsoft.Extensions.Resilience / Polly.
/// </summary>
/// <remarks>
/// This is the right middleware level for agent resilience because it wraps the entire
/// <c>RunAsync()</c> call and catches LLM failures, tool failures, and orchestration errors
/// together. Streaming <c>RunStreamingAsync()</c> passes through without retry.
/// </remarks>
public sealed class AgentResiliencePlugin : IAIAgentBuilderPlugin
{
    private readonly ResiliencePipeline<AgentResponse> _pipeline;

    /// <param name="pipeline">
    /// The resilience pipeline to wrap around each <c>RunAsync</c> call.
    /// </param>
    public AgentResiliencePlugin(ResiliencePipeline<AgentResponse> pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        _pipeline = pipeline;
    }

    /// <inheritdoc />
    public void Configure(AIAgentBuilderPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AgentBuilder.Use(
            // Non-streaming: wrap in resilience pipeline.
            async (messages, session, runOptions, innerAgent, cancellationToken) =>
                await _pipeline.ExecuteAsync(
                    async ct => await innerAgent.RunAsync(messages, session, runOptions, ct).ConfigureAwait(false),
                    cancellationToken)
                .ConfigureAwait(false),

            // Streaming: pass through without retry — retrying a partially-consumed stream
            // is not meaningful.
            (messages, session, runOptions, innerAgent, cancellationToken) =>
                innerAgent.RunStreamingAsync(messages, session, runOptions, cancellationToken));
    }
}
