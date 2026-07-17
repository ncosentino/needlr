using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseScoreClient"/> registered when Langfuse is not configured, so host
/// applications can always inject the client and call it without branching on configuration state.
/// </summary>
[DoNotAutoRegister]
internal sealed class DisabledLangfuseScoreClient : ILangfuseScoreClient
{
    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public Task RecordScoreAsync(string traceId, string name, double value) =>
        RecordScoreAsync(traceId, name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordScoreAsync(
        string traceId,
        string name,
        double value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordScoreAsync(string traceId, string name, bool value) =>
        RecordScoreAsync(traceId, name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordScoreAsync(
        string traceId,
        string name,
        bool value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordScoreAsync(string traceId, string name, string value) =>
        RecordScoreAsync(traceId, name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordScoreAsync(
        string traceId,
        string name,
        string value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordEvaluationAsync(string traceId, EvaluationResult result) =>
        RecordEvaluationAsync(traceId, result, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordEvaluationAsync(
        string traceId,
        EvaluationResult result,
        LangfuseEvaluationScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordObservationScoreAsync(
        string traceId,
        string observationId,
        string name,
        double value) =>
        RecordObservationScoreAsync(
            traceId,
            observationId,
            name,
            value,
            options: null,
            CancellationToken.None);

    /// <inheritdoc />
    public Task RecordObservationScoreAsync(
        string traceId,
        string observationId,
        string name,
        double value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordObservationScoreAsync(
        string traceId,
        string observationId,
        string name,
        bool value) =>
        RecordObservationScoreAsync(
            traceId,
            observationId,
            name,
            value,
            options: null,
            CancellationToken.None);

    /// <inheritdoc />
    public Task RecordObservationScoreAsync(
        string traceId,
        string observationId,
        string name,
        bool value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordObservationScoreAsync(
        string traceId,
        string observationId,
        string name,
        string value) =>
        RecordObservationScoreAsync(
            traceId,
            observationId,
            name,
            value,
            options: null,
            CancellationToken.None);

    /// <inheritdoc />
    public Task RecordObservationScoreAsync(
        string traceId,
        string observationId,
        string name,
        string value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string sessionId, string name, double value) =>
        RecordSessionScoreAsync(sessionId, name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(
        string sessionId,
        string name,
        double value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string sessionId, string name, bool value) =>
        RecordSessionScoreAsync(sessionId, name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(
        string sessionId,
        string name,
        bool value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string sessionId, string name, string value) =>
        RecordSessionScoreAsync(sessionId, name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(
        string sessionId,
        string name,
        string value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
