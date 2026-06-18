using System.Diagnostics;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseScenario"/> returned by a disabled session. Starts no span and posts
/// no scores, so eval code paths run unchanged when Langfuse is not configured.
/// </summary>
internal sealed class DisabledLangfuseScenario : ILangfuseScenario
{
    /// <inheritdoc />
    public string? TraceId => null;

    /// <inheritdoc />
    public Activity? Activity => null;

    /// <inheritdoc />
    public Task RecordScoreAsync(string name, double value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordScoreAsync(string name, bool value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordScoreAsync(string name, string value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordEvaluationAsync(EvaluationResult result, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public void SetTracePublic(bool isPublic = true)
    {
    }

    /// <inheritdoc />
    public void SetVersion(string version)
    {
    }

    /// <inheritdoc />
    public void SetInput(object input)
    {
    }

    /// <inheritdoc />
    public void SetOutput(object output)
    {
    }

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string name, double value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string name, bool value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string name, string value, string? comment = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
