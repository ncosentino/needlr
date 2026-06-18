using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Records Langfuse scores against a known trace id. Registered in dependency injection by
/// <see cref="LangfuseServiceCollectionExtensions.AddNeedlrLangfuse"/> for ASP.NET Core and
/// generic-host applications that score their own request traces.
/// </summary>
/// <remarks>
/// In a host application the OTLP exporter assigns each operation an OpenTelemetry trace id
/// (<c>Activity.Current?.TraceId</c>). Pass that id here to attach evaluation scores to the
/// corresponding Langfuse trace. For the eval/console flow, prefer
/// <see cref="ILangfuseSession.BeginScenario"/>, which manages the trace for you.
/// </remarks>
public interface ILangfuseScoreClient
{
    /// <summary>
    /// Gets a value indicating whether scores are being sent. <see langword="false"/> when Langfuse
    /// was not configured, in which case all record calls are no-ops.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>Gets the cumulative number of score uploads that have failed.</summary>
    int ScoresFailed { get; }

    /// <summary>Records a numeric score against <paramref name="traceId"/>.</summary>
    /// <param name="traceId">The Langfuse/OpenTelemetry trace id to attach the score to.</param>
    /// <param name="name">The score name.</param>
    /// <param name="value">The numeric value.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordScoreAsync(string traceId, string name, double value, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>Records a boolean score (stored as <c>1</c>/<c>0</c>) against <paramref name="traceId"/>.</summary>
    /// <param name="traceId">The Langfuse/OpenTelemetry trace id to attach the score to.</param>
    /// <param name="name">The score name.</param>
    /// <param name="value">The boolean value.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordScoreAsync(string traceId, string name, bool value, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>Records a categorical score against <paramref name="traceId"/>.</summary>
    /// <param name="traceId">The Langfuse/OpenTelemetry trace id to attach the score to.</param>
    /// <param name="name">The score name.</param>
    /// <param name="value">The category label.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordScoreAsync(string traceId, string name, string value, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects every metric in <paramref name="result"/> to a Langfuse score against
    /// <paramref name="traceId"/>, using the same mapping as
    /// <see cref="ILangfuseScenario.RecordEvaluationAsync"/>.
    /// </summary>
    /// <param name="traceId">The Langfuse/OpenTelemetry trace id to attach the scores to.</param>
    /// <param name="result">The evaluation result to project.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted all projected scores.</returns>
    Task RecordEvaluationAsync(string traceId, EvaluationResult result, CancellationToken cancellationToken = default);

    /// <summary>Records a numeric score against a specific observation within a trace.</summary>
    /// <param name="traceId">The owning trace id.</param>
    /// <param name="observationId">The observation (span/generation) id to attach the score to.</param>
    /// <param name="name">The score name.</param>
    /// <param name="value">The numeric value.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordObservationScoreAsync(string traceId, string observationId, string name, double value, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>Records a boolean score (stored as <c>1</c>/<c>0</c>) against a specific observation.</summary>
    /// <param name="traceId">The owning trace id.</param>
    /// <param name="observationId">The observation id to attach the score to.</param>
    /// <param name="name">The score name.</param>
    /// <param name="value">The boolean value.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordObservationScoreAsync(string traceId, string observationId, string name, bool value, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>Records a categorical score against a specific observation.</summary>
    /// <param name="traceId">The owning trace id.</param>
    /// <param name="observationId">The observation id to attach the score to.</param>
    /// <param name="name">The score name.</param>
    /// <param name="value">The category label.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordObservationScoreAsync(string traceId, string observationId, string name, string value, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>Records a numeric score against a whole session (across its traces).</summary>
    /// <param name="sessionId">The session id to attach the score to.</param>
    /// <param name="name">The score name.</param>
    /// <param name="value">The numeric value.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordSessionScoreAsync(string sessionId, string name, double value, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>Records a boolean session score (stored as <c>1</c>/<c>0</c>).</summary>
    /// <param name="sessionId">The session id to attach the score to.</param>
    /// <param name="name">The score name.</param>
    /// <param name="value">The boolean value.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordSessionScoreAsync(string sessionId, string name, bool value, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>Records a categorical session score.</summary>
    /// <param name="sessionId">The session id to attach the score to.</param>
    /// <param name="name">The score name.</param>
    /// <param name="value">The category label.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted the score.</returns>
    Task RecordSessionScoreAsync(string sessionId, string name, string value, string? comment = null, CancellationToken cancellationToken = default);
}
