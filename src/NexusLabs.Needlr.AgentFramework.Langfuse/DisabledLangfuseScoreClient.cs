using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseScoreClient"/> registered when Langfuse is not configured, so host
/// applications can always inject the client and call it without branching on configuration state.
/// </summary>
internal sealed class DisabledLangfuseScoreClient : ILangfuseScoreClient
{
    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public int ScoresFailed => 0;

    /// <inheritdoc />
    public Task RecordScoreAsync(string traceId, string name, double value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordScoreAsync(string traceId, string name, bool value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordScoreAsync(string traceId, string name, string value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordEvaluationAsync(string traceId, EvaluationResult result, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordObservationScoreAsync(string traceId, string observationId, string name, double value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordObservationScoreAsync(string traceId, string observationId, string name, bool value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordObservationScoreAsync(string traceId, string observationId, string name, string value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string sessionId, string name, double value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string sessionId, string name, bool value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string sessionId, string name, string value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
