using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Extension methods for opting in to <see cref="EvaluationCaptureChatClient"/>
/// capture/replay behavior.
/// </summary>
public static class EvaluationCaptureChatClientExtensions
{
    /// <summary>
    /// Wraps <paramref name="innerClient"/> with an
    /// <see cref="EvaluationCaptureChatClient"/> backed by
    /// <paramref name="store"/>.
    /// </summary>
    /// <param name="innerClient">The chat client to delegate cache-miss calls to.</param>
    /// <param name="store">Backing store for captured responses.</param>
    /// <returns>A new <see cref="IChatClient"/> that captures and replays responses.</returns>
    public static IChatClient WithEvaluationCapture(
        this IChatClient innerClient,
        IEvaluationCaptureStore store)
    {
        ArgumentNullException.ThrowIfNull(innerClient);
        ArgumentNullException.ThrowIfNull(store);
        return new EvaluationCaptureChatClient(innerClient, store);
    }

    /// <summary>
    /// Wraps <paramref name="innerClient"/> with an
    /// <see cref="EvaluationCaptureChatClient"/> backed by a
    /// <see cref="FileEvaluationCaptureStore"/> rooted at
    /// <paramref name="cacheDirectory"/>.
    /// </summary>
    /// <param name="innerClient">The chat client to delegate cache-miss calls to.</param>
    /// <param name="cacheDirectory">Directory used to persist captured responses.</param>
    /// <returns>A new <see cref="IChatClient"/> that captures and replays responses.</returns>
    public static IChatClient WithEvaluationCapture(
        this IChatClient innerClient,
        string cacheDirectory)
    {
        ArgumentNullException.ThrowIfNull(innerClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        return new EvaluationCaptureChatClient(
            innerClient,
            new FileEvaluationCaptureStore(cacheDirectory));
    }
}
