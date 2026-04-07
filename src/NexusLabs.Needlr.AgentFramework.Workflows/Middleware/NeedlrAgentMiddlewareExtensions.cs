using Microsoft.Agents.AI;

using NexusLabs.Needlr.AgentFramework;

using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Middleware;

/// <summary>
/// Extension methods that add Needlr middleware to the <see cref="AgentFrameworkSyringe"/>
/// and to MAF's <see cref="AIAgentBuilder"/> directly.
/// </summary>
public static class NeedlrAgentMiddlewareExtensions
{
    // ─── AgentFrameworkSyringe extensions ────────────────────────────────────

    /// <summary>
    /// Adds <see cref="ToolResultFunctionMiddleware"/> to every agent created by the factory.
    /// </summary>
    /// <remarks>
    /// This ensures that all <c>[AgentFunction]</c> methods return a safe, structured JSON
    /// payload to the LLM — even when they throw an unhandled exception.
    /// </remarks>
    public static AgentFrameworkSyringe UsingToolResultMiddleware(
        this AgentFrameworkSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return syringe with
        {
            Plugins = (syringe.Plugins ?? []).Append(new ToolResultFunctionMiddleware()).ToList()
        };
    }

    /// <summary>
    /// Wraps every agent created by the factory with a default resilience pipeline:
    /// 2 retries with exponential back-off and a 120-second timeout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per-agent settings can be overridden by applying <see cref="AgentResilienceAttribute"/>
    /// to the agent class.
    /// </para>
    /// <para>
    /// For a custom pipeline use the <see cref="UsingResilience(AgentFrameworkSyringe, Action{ResiliencePipelineBuilder{AgentResponse}})"/>
    /// overload.
    /// </para>
    /// </remarks>
    public static AgentFrameworkSyringe UsingResilience(
        this AgentFrameworkSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return syringe.UsingResilience(builder =>
        {
            builder
                .AddRetry(new RetryStrategyOptions<AgentResponse>
                {
                    MaxRetryAttempts = 2,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder<AgentResponse>()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>()
                        .Handle<OperationCanceledException>()
                })
                .AddTimeout(TimeSpan.FromSeconds(120));
        });
    }

    /// <summary>
    /// Wraps every agent created by the factory with a custom resilience pipeline.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="configure">Callback to configure the <see cref="ResiliencePipelineBuilder{TResult}"/>.</param>
    public static AgentFrameworkSyringe UsingResilience(
        this AgentFrameworkSyringe syringe,
        Action<ResiliencePipelineBuilder<AgentResponse>> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ResiliencePipelineBuilder<AgentResponse>();
        configure(builder);
        var globalPipeline = builder.Build();

        return syringe with
        {
            Plugins = (syringe.Plugins ?? []).Append(new AgentResiliencePlugin(globalPipeline)).ToList(),
            PerAgentResilienceFactory = attr => BuildPerAgentPlugin(attr)
        };
    }

    // ─── AIAgentBuilder extensions (any-agent, not just Needlr-managed) ──────

    /// <summary>
    /// Adds <see cref="ToolResultFunctionMiddleware"/> to an <see cref="AIAgentBuilder"/>
    /// directly, for agents not managed by Needlr.
    /// </summary>
    public static AIAgentBuilder UseToolResultMiddleware(this AIAgentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var plugin = new ToolResultFunctionMiddleware();
        plugin.Configure(new AIAgentBuilderPluginOptions { AgentBuilder = builder });
        return builder;
    }

    /// <summary>
    /// Adds a default resilience pipeline to an <see cref="AIAgentBuilder"/>
    /// directly, for agents not managed by Needlr.
    /// </summary>
    public static AIAgentBuilder UseResilience(this AIAgentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var pipeline = new ResiliencePipelineBuilder<AgentResponse>()
            .AddRetry(new RetryStrategyOptions<AgentResponse>
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<AgentResponse>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<OperationCanceledException>()
            })
            .AddTimeout(TimeSpan.FromSeconds(120))
            .Build();

        var plugin = new AgentResiliencePlugin(pipeline);
        plugin.Configure(new AIAgentBuilderPluginOptions { AgentBuilder = builder });
        return builder;
    }

    /// <summary>
    /// Adds a custom resilience pipeline to an <see cref="AIAgentBuilder"/>
    /// directly, for agents not managed by Needlr.
    /// </summary>
    public static AIAgentBuilder UseResilience(
        this AIAgentBuilder builder,
        ResiliencePipeline<AgentResponse> pipeline)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(pipeline);

        var plugin = new AgentResiliencePlugin(pipeline);
        plugin.Configure(new AIAgentBuilderPluginOptions { AgentBuilder = builder });
        return builder;
    }

    // ─── Internal helpers ────────────────────────────────────────────────────

    private static AgentResiliencePlugin BuildPerAgentPlugin(AgentResilienceAttribute attr)
    {
        var pipelineBuilder = new ResiliencePipelineBuilder<AgentResponse>();

        if (attr.MaxRetries > 0)
        {
            pipelineBuilder.AddRetry(new RetryStrategyOptions<AgentResponse>
            {
                MaxRetryAttempts = attr.MaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<AgentResponse>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<OperationCanceledException>()
            });
        }

        if (attr.TimeoutSeconds > 0)
        {
            pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(attr.TimeoutSeconds));
        }

        return new AgentResiliencePlugin(pipelineBuilder.Build());
    }
}
