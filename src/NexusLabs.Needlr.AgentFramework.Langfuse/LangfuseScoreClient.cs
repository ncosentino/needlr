using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Default <see cref="ILangfuseScoreClient"/> that records scores via a shared
/// <see cref="LangfuseScoreRecorder"/>.
/// </summary>
internal sealed class LangfuseScoreClient : ILangfuseScoreClient
{
    private readonly LangfuseScoreRecorder _recorder;
    private readonly LangfuseScoreFailureSink _failureSink;

    public LangfuseScoreClient(LangfuseScoreRecorder recorder, LangfuseScoreFailureSink failureSink)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(failureSink);

        _recorder = recorder;
        _failureSink = failureSink;
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public int ScoresFailed => _failureSink.FailedCount;

    /// <inheritdoc />
    public Task RecordScoreAsync(string traceId, string name, double value, string? comment = null, CancellationToken cancellationToken = default) =>
        _recorder.RecordNumericAsync(traceId, name, value, comment, cancellationToken);

    /// <inheritdoc />
    public Task RecordScoreAsync(string traceId, string name, bool value, string? comment = null, CancellationToken cancellationToken = default) =>
        _recorder.RecordBooleanAsync(traceId, name, value, comment, cancellationToken);

    /// <inheritdoc />
    public Task RecordScoreAsync(string traceId, string name, string value, string? comment = null, CancellationToken cancellationToken = default) =>
        _recorder.RecordCategoricalAsync(traceId, name, value, comment, cancellationToken);

    /// <inheritdoc />
    public Task RecordEvaluationAsync(string traceId, EvaluationResult result, CancellationToken cancellationToken = default) =>
        _recorder.RecordEvaluationAsync(traceId, result, cancellationToken);
}
