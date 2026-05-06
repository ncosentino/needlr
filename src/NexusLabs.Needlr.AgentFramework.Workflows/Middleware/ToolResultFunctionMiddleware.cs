using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Tools;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Middleware;

/// <summary>
/// MAF function-invocation middleware that intercepts <c>[AgentFunction]</c> return values and
/// exceptions, ensuring the LLM always receives a structured JSON response instead of a raw
/// stack trace.
/// </summary>
/// <remarks>
/// <para>
/// When an <c>[AgentFunction]</c> method returns a <see cref="IToolResult"/>:
/// <list type="bullet">
/// <item>
/// <description>Success — the LLM receives the <see cref="IToolResult.BoxedValue"/> directly.</description>
/// </item>
/// <item>
/// <description>
/// Failure — the LLM receives <c>{ "error": { … } }</c> (the <see cref="IToolResult.BoxedError"/>
/// wrapped), and the original <see cref="Exception"/> is preserved on <see cref="IToolResult.Exception"/>
/// for diagnostics.
/// </description>
/// </item>
/// </list>
/// </para>
/// <para>
/// When an <c>[AgentFunction]</c> throws an <em>unhandled</em> exception, the middleware catches it,
/// wraps it in an <see cref="ToolResult.UnhandledFailure"/> result, and returns a safe generic error
/// message to the LLM. <see cref="IToolResult.IsTransient"/> is <see langword="null"/> in this case.
/// </para>
/// <para>
/// <see cref="OperationCanceledException"/> is intentionally <em>not</em> caught — it propagates so
/// cooperative cancellation (parent timeouts, user cancels, structured-concurrency aborts) continues
/// to function correctly. Tools that legitimately catch and translate cancellation should do so
/// inside the tool body, not rely on this middleware.
/// </para>
/// <para>
/// Non-<see cref="IToolResult"/> return values pass through unchanged.
/// </para>
/// </remarks>
public sealed class ToolResultFunctionMiddleware : IAIAgentBuilderPlugin
{
    /// <inheritdoc />
    public void Configure(AIAgentBuilderPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        FunctionInvocationDelegatingAgentBuilderExtensions.Use(
            options.AgentBuilder,
            async (agent, context, next, cancellationToken) =>
                await HandleInvocationAsync(
                    invokeNext: ct => next(context, ct),
                    cancellationToken: cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Core middleware logic: invoke <paramref name="invokeNext"/>, translate exceptions into
    /// <see cref="IToolResult"/> failures, and unwrap <see cref="IToolResult"/> returns into
    /// LLM-facing <see cref="IToolResult.BoxedValue"/> or <c>{ error: BoxedError }</c>.
    /// Cooperative <see cref="OperationCanceledException"/> propagates unchanged so cancellation
    /// signals are not swallowed.
    /// </summary>
    /// <remarks>
    /// Internal-but-exposed-via-<c>InternalsVisibleTo</c> for direct unit testing — exercising the
    /// translation logic without standing up a full agent pipeline.
    /// </remarks>
    internal static async ValueTask<object?> HandleInvocationAsync(
        Func<CancellationToken, ValueTask<object?>> invokeNext,
        CancellationToken cancellationToken)
    {
        object? raw;

        try
        {
            raw = await invokeNext(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            raw = ToolResult.UnhandledFailure(ex);
        }

        if (raw is IToolResult result)
        {
            return result.IsSuccess
                ? result.BoxedValue
                : new { error = result.BoxedError };
        }

        return raw;
    }
}
