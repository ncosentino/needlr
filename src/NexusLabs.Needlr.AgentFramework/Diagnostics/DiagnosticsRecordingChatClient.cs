using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// <see cref="DelegatingChatClient"/> that routes calls through a
/// <see cref="DiagnosticsChatClientMiddleware"/> and is detectable via
/// <c>GetService</c>. Because this extends
/// <see cref="DelegatingChatClient"/>, MEAI's <c>GetService</c> walks the
/// delegation chain and returns this instance when queried by type — enabling
/// idempotent middleware installation.
/// </summary>
/// <remarks>
/// <para>
/// Both <c>UsingDiagnostics()</c> and <c>IterativeAgentLoop</c> use this class
/// to wrap the chat client. Before wrapping, the loop checks
/// <c>chatClient.GetService&lt;DiagnosticsRecordingChatClient&gt;()</c> — if
/// one already exists in the pipeline, it skips installation. This makes it
/// structurally impossible for two diagnostics middlewares to both record
/// <see cref="ChatCompletionDiagnostics"/> to the same
/// <see cref="AgentRunDiagnosticsBuilder"/>.
/// </para>
/// </remarks>
[DoNotAutoRegister]
public sealed class DiagnosticsRecordingChatClient : DelegatingChatClient
{
    private readonly DiagnosticsChatClientMiddleware? _middleware;

    /// <summary>
    /// Creates a new diagnostics-recording wrapper around the specified client.
    /// If the inner client chain already contains a
    /// <see cref="DiagnosticsRecordingChatClient"/>, this instance becomes a
    /// passthrough — it delegates directly without invoking the middleware,
    /// guaranteeing at most one active recorder per pipeline.
    /// </summary>
    /// <param name="innerClient">The inner client to delegate to.</param>
    /// <param name="middleware">The middleware that records chat completions.</param>
    internal DiagnosticsRecordingChatClient(
        IChatClient innerClient,
        DiagnosticsChatClientMiddleware middleware)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        // If the inner chain already contains a DiagnosticsRecordingChatClient,
        // become a passthrough. This makes triple/N-wrapping safe — only the
        // innermost instance records, all outer instances are no-ops.
        if (innerClient.GetService<DiagnosticsRecordingChatClient>() is not null)
        {
            _middleware = null;
        }
        else
        {
            _middleware = middleware;
        }
    }

    /// <inheritdoc />
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        if (_middleware is null)
        {
            return base.GetResponseAsync(messages, options, cancellationToken);
        }

        return _middleware.HandleAsync(messages, options, InnerClient, cancellationToken);
    }

    /// <inheritdoc />
    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        if (_middleware is null)
        {
            return base.GetStreamingResponseAsync(messages, options, cancellationToken);
        }

        return _middleware.HandleStreamingAsync(messages, options, InnerClient, cancellationToken);
    }
}
