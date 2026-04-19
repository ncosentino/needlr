using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Extension methods for wiring <see cref="DiagnosticsFunctionInvokingChatClient"/>
/// into an MEAI chat client pipeline.
/// </summary>
public static class DiagnosticsFunctionInvocationExtensions
{
    /// <summary>
    /// Inserts a <see cref="DiagnosticsFunctionInvokingChatClient"/> into the pipeline
    /// that records per-tool-call diagnostics, OTel metrics, and Activity spans.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaces the standard <c>UseFunctionInvocation()</c> call. Do not use both —
    /// it would create two <c>FunctionInvokingChatClient</c> layers and produce
    /// duplicate tool calls.
    /// </para>
    /// </remarks>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="metrics">Optional OTel metrics recorder.</param>
    /// <param name="progressAccessor">Optional progress reporter for real-time events.</param>
    /// <returns>The builder, for chaining.</returns>
    public static ChatClientBuilder UseDiagnosticsFunctionInvocation(
        this ChatClientBuilder builder,
        IAgentMetrics? metrics = null,
        IProgressReporterAccessor? progressAccessor = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.Use(innerClient =>
            new DiagnosticsFunctionInvokingChatClient(
                innerClient, metrics, progressAccessor));
    }
}
