using System.Diagnostics;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Represents a single Langfuse trace scoped to one eval scenario or agent run. Created via
/// <see cref="ILangfuseClient.BeginScenario"/>.
/// </summary>
/// <remarks>
/// <para>
/// Beginning a scenario starts a root OpenTelemetry span carrying Langfuse trace-level attributes
/// (name, session id, user id, tags, metadata). Agent telemetry produced while the scenario is
/// active nests under this trace. Disposing the scenario ends the root span.
/// </para>
/// <para>
/// Evaluation scores are attached to the scenario's trace through the Langfuse Scores API, keyed
/// by <see cref="TraceId"/>. Because Langfuse links scores to traces asynchronously, scores may be
/// recorded while the scenario is still open.
/// </para>
/// </remarks>
public interface ILangfuseScenario : IDisposable
{
    /// <summary>
    /// Gets the Langfuse trace id (the lowercase-hex OpenTelemetry trace id) for this scenario, or
    /// <see langword="null"/> when the owning session is disabled.
    /// </summary>
    string? TraceId { get; }

    /// <summary>
    /// Gets the root <see cref="Activity"/> for this scenario, or <see langword="null"/> when the
    /// owning session is disabled or no listener is sampling the span.
    /// </summary>
    Activity? Activity { get; }

    /// <summary>
    /// Records a numeric score on this scenario's trace.
    /// </summary>
    /// <param name="name">The score name.</param>
    /// <param name="value">The numeric value.</param>
    /// <param name="options">Optional score identity and comment settings.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordScoreAsync(string name, double value, LangfuseScoreOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a boolean score (stored as <c>1</c>/<c>0</c>) on this scenario's trace.
    /// </summary>
    /// <param name="name">The score name.</param>
    /// <param name="value">The boolean value.</param>
    /// <param name="options">Optional score identity and comment settings.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordScoreAsync(string name, bool value, LangfuseScoreOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a categorical score on this scenario's trace.
    /// </summary>
    /// <param name="name">The score name.</param>
    /// <param name="value">The category label.</param>
    /// <param name="options">Optional score identity and comment settings.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordScoreAsync(string name, string value, LangfuseScoreOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects every metric in a <see cref="EvaluationResult"/> to a Langfuse score on this
    /// scenario's trace. Numeric metrics become numeric scores, boolean metrics become boolean
    /// scores, and string metrics become categorical scores. Each metric's reason is sent as the
    /// score comment. Metrics whose value is unset are skipped.
    /// </summary>
    /// <param name="result">The evaluation result to project.</param>
    /// <param name="options">Optional stable identity settings for projected metric scores.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted all projected scores.</returns>
    Task RecordEvaluationAsync(
        EvaluationResult result,
        LangfuseEvaluationScoreOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks this scenario's trace as public so Langfuse exposes it via a shareable URL that needs
    /// no login — useful for linking a failing eval trace in a pull request. No-op when the owning
    /// session is disabled.
    /// </summary>
    /// <param name="isPublic">Whether the trace is public. Defaults to <see langword="true"/>.</param>
    void SetTracePublic(bool isPublic = true);

    /// <summary>
    /// Sets the version of this scenario's trace (for example a git SHA or prompt/logic version),
    /// emitted as <c>langfuse.version</c>. No-op when the owning session is disabled.
    /// </summary>
    /// <param name="version">The version identifier.</param>
    void SetVersion(string version);

    /// <summary>
    /// Sets the trace-level input shown at the top of the trace in Langfuse. Strings are stored
    /// verbatim; other values are serialized to JSON. No-op when the owning session is disabled.
    /// </summary>
    /// <param name="input">The input value (typically the eval's prompt or request).</param>
    void SetInput(object input);

    /// <summary>
    /// Sets the trace-level output shown at the top of the trace in Langfuse. Strings are stored
    /// verbatim; other values are serialized to JSON. Typically called with the agent's final
    /// answer before the scenario is disposed. No-op when the owning session is disabled.
    /// </summary>
    /// <param name="output">The output value (typically the agent's final answer).</param>
    void SetOutput(object output);

    /// <summary>
    /// Links generations produced under this scenario to a versioned prompt managed in Langfuse, by
    /// stamping <c>langfuse.observation.prompt.name</c> / <c>version</c> on the chat-completion
    /// (generation) spans. Tool spans are unaffected. Call before running the agent. No-op when the
    /// owning session is disabled.
    /// </summary>
    /// <param name="name">The managed prompt name (as it appears in Langfuse prompt management).</param>
    /// <param name="version">The prompt version, or <see langword="null"/> to link by name only.</param>
    void SetPrompt(string name, int? version = null);

    /// <summary>
    /// Links generations under this scenario to a prompt fetched from Langfuse prompt management,
    /// using its name and version. No-op when the owning session is disabled.
    /// </summary>
    /// <param name="prompt">The fetched prompt.</param>
    void SetPrompt(LangfusePrompt prompt);

    /// <summary>
    /// Records a numeric score against the session this scenario belongs to (the session id passed
    /// when the scenario began), scoring the whole multi-turn conversation rather than this single
    /// trace. Surfaced as a non-fatal skip when the scenario has no session id.
    /// </summary>
    /// <param name="name">The score name.</param>
    /// <param name="value">The numeric value.</param>
    /// <param name="options">Optional score identity and comment settings.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordSessionScoreAsync(string name, double value, LangfuseScoreOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Records a boolean session score (stored as <c>1</c>/<c>0</c>) for this scenario's session.</summary>
    /// <param name="name">The score name.</param>
    /// <param name="value">The boolean value.</param>
    /// <param name="options">Optional score identity and comment settings.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordSessionScoreAsync(string name, bool value, LangfuseScoreOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Records a categorical session score for this scenario's session.</summary>
    /// <param name="name">The score name.</param>
    /// <param name="value">The category label.</param>
    /// <param name="options">Optional score identity and comment settings.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordSessionScoreAsync(string name, string value, LangfuseScoreOptions? options = null, CancellationToken cancellationToken = default);
}
