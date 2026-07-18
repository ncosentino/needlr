using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Default <see cref="ILangfuseScoreClient"/> that records scores via a shared
/// <see cref="LangfuseScoreRecorder"/>.
/// </summary>
[DoNotAutoRegister]
internal sealed class LangfuseScoreClient : ILangfuseScoreClient
{
    private readonly LangfuseScoreRecorder _recorder;

    public LangfuseScoreClient(
        LangfuseScoreRecorder recorder,
        LangfuseScoreFailureSink failureSink)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(failureSink);

        _recorder = recorder;
        PublicationHealth = recorder.Health;
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    internal LangfusePublicationHealth PublicationHealth { get; }

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
        _recorder.RecordNumericAsync(traceId, name, value, options, cancellationToken);

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
        _recorder.RecordBooleanAsync(traceId, name, value, options, cancellationToken);

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
        _recorder.RecordCategoricalAsync(traceId, name, value, options, cancellationToken);

    /// <inheritdoc />
    public Task RecordEvaluationAsync(
        string traceId,
        EvaluationResult result) =>
        RecordEvaluationAsync(traceId, result, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordEvaluationAsync(
        string traceId,
        EvaluationResult result,
        LangfuseEvaluationScoreOptions? options,
        CancellationToken cancellationToken) =>
        _recorder.RecordEvaluationAsync(traceId, result, options, cancellationToken);

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
        _recorder.RecordNumericAsync(LangfuseScoreTarget.Observation(traceId, observationId), name, value, options, cancellationToken);

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
        _recorder.RecordBooleanAsync(LangfuseScoreTarget.Observation(traceId, observationId), name, value, options, cancellationToken);

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
        _recorder.RecordCategoricalAsync(LangfuseScoreTarget.Observation(traceId, observationId), name, value, options, cancellationToken);

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
        _recorder.RecordNumericAsync(LangfuseScoreTarget.Session(sessionId), name, value, options, cancellationToken);

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
        _recorder.RecordBooleanAsync(LangfuseScoreTarget.Session(sessionId), name, value, options, cancellationToken);

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
        _recorder.RecordCategoricalAsync(LangfuseScoreTarget.Session(sessionId), name, value, options, cancellationToken);
}
