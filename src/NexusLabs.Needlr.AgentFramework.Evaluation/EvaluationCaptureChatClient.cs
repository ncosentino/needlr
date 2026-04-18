using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// <see cref="DelegatingChatClient"/> that persists every LLM request/response pair
/// to an <see cref="IEvaluationCaptureStore"/> and replays cached responses on
/// subsequent calls with an identical request. Intended to make evaluator runs
/// deterministic and cheap to re-execute.
/// </summary>
/// <remarks>
/// <para>
/// Cache keys are derived from a stable SHA-256 hash of the request messages
/// (role + text) and a small subset of <see cref="ChatOptions"/> that affect
/// output — currently <c>ModelId</c>, <c>Temperature</c>, <c>TopP</c>, and
/// <c>MaxOutputTokens</c>. Requests that differ only in non-captured options
/// will collide; callers that rely on other options producing distinct responses
/// must not use this middleware.
/// </para>
/// <para>
/// Streaming calls materialize cached responses as a single
/// <see cref="ChatResponseUpdate"/> per message. On cache miss the stream is
/// fully buffered before being persisted and replayed to the caller.
/// </para>
/// </remarks>
public sealed class EvaluationCaptureChatClient : DelegatingChatClient
{
    private readonly IEvaluationCaptureStore _store;

    /// <param name="innerClient">The inner chat client to delegate to.</param>
    /// <param name="store">Backing store used for capture and replay.</param>
    public EvaluationCaptureChatClient(
        IChatClient innerClient,
        IEvaluationCaptureStore store)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var materialized = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var key = ComputeKey(materialized, options);

        var cached = await _store.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var response = await base
            .GetResponseAsync(materialized, options, cancellationToken)
            .ConfigureAwait(false);

        await _store.SaveAsync(key, response, cancellationToken).ConfigureAwait(false);
        return response;
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var materialized = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var key = ComputeKey(materialized, options);

        var cached = await _store.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            foreach (var update in ToUpdates(cached))
            {
                yield return update;
            }
            yield break;
        }

        var buffered = new List<ChatResponseUpdate>();
        await foreach (var update in base
            .GetStreamingResponseAsync(materialized, options, cancellationToken)
            .ConfigureAwait(false))
        {
            buffered.Add(update);
            yield return update;
        }

        var combined = buffered.ToChatResponse();
        await _store.SaveAsync(key, combined, cancellationToken).ConfigureAwait(false);
    }

    internal static string ComputeKey(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options)
    {
        var sb = new StringBuilder();
        foreach (var message in messages)
        {
            sb.Append(message.Role.Value);
            sb.Append(':');
            sb.Append(message.Text);
            sb.Append('\n');
        }

        sb.Append("---\n");
        sb.Append("model:").Append(options?.ModelId ?? string.Empty).Append('\n');
        sb.Append("temp:").Append(FormatNullable(options?.Temperature)).Append('\n');
        sb.Append("topp:").Append(FormatNullable(options?.TopP)).Append('\n');
        sb.Append("max:").Append(options?.MaxOutputTokens?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append('\n');

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string FormatNullable(float? value) =>
        value.HasValue
            ? value.Value.ToString("R", CultureInfo.InvariantCulture)
            : string.Empty;

    private static IEnumerable<ChatResponseUpdate> ToUpdates(ChatResponse response)
    {
        foreach (var message in response.Messages)
        {
            yield return new ChatResponseUpdate(message.Role, message.Text)
            {
                ResponseId = response.ResponseId,
                ModelId = response.ModelId,
            };
        }
    }
}
