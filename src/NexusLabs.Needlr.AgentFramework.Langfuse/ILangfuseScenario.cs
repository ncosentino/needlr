using System.Diagnostics;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Represents a single Langfuse trace scoped to one eval scenario or agent run. Created via
/// <see cref="ILangfuseSession.BeginScenario"/>.
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
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordScoreAsync(string name, double value, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a boolean score (stored as <c>1</c>/<c>0</c>) on this scenario's trace.
    /// </summary>
    /// <param name="name">The score name.</param>
    /// <param name="value">The boolean value.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordScoreAsync(string name, bool value, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a categorical score on this scenario's trace.
    /// </summary>
    /// <param name="name">The score name.</param>
    /// <param name="value">The category label.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordScoreAsync(string name, string value, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects every metric in a <see cref="EvaluationResult"/> to a Langfuse score on this
    /// scenario's trace. Numeric metrics become numeric scores, boolean metrics become boolean
    /// scores, and string metrics become categorical scores. Each metric's reason is sent as the
    /// score comment. Metrics whose value is unset are skipped.
    /// </summary>
    /// <param name="result">The evaluation result to project.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted all projected scores.</returns>
    Task RecordEvaluationAsync(EvaluationResult result, CancellationToken cancellationToken = default);
}
