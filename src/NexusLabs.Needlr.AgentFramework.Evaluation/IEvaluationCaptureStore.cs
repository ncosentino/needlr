using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Persists captured <see cref="ChatResponse"/> payloads keyed by a deterministic
/// request hash so that evaluator runs can replay previously observed LLM responses
/// without re-invoking the underlying model.
/// </summary>
/// <remarks>
/// Implementations must be safe to call concurrently. Keys are opaque strings
/// produced by <see cref="EvaluationCaptureChatClient"/>; callers should not
/// construct or parse them directly.
/// </remarks>
public interface IEvaluationCaptureStore
{
    /// <summary>
    /// Looks up a previously captured response for <paramref name="key"/>.
    /// </summary>
    /// <param name="key">Opaque request hash produced by the capture client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The cached <see cref="ChatResponse"/> when present, otherwise <see langword="null"/>.
    /// </returns>
    Task<ChatResponse?> TryGetAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Persists <paramref name="response"/> under <paramref name="key"/>.
    /// Overwrites any existing entry for the same key.
    /// </summary>
    /// <param name="key">Opaque request hash produced by the capture client.</param>
    /// <param name="response">The response to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(string key, ChatResponse response, CancellationToken cancellationToken);
}
