using System.Diagnostics;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseScenario"/> returned by a disabled session. Starts no span and posts
/// no scores, so eval code paths run unchanged when Langfuse is not configured.
/// </summary>
[DoNotAutoRegister]
internal sealed class DisabledLangfuseScenario : ILangfuseActivatableScenario
{
    /// <inheritdoc />
    public string? TraceId => null;

    /// <inheritdoc />
    public Activity? Activity => null;

    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public IDisposable? Activate() => null;

    /// <inheritdoc />
    public Task RecordScoreAsync(string name, double value) =>
        RecordScoreAsync(name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordScoreAsync(
        string name,
        double value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordScoreAsync(string name, bool value) =>
        RecordScoreAsync(name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordScoreAsync(
        string name,
        bool value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordScoreAsync(string name, string value) =>
        RecordScoreAsync(name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordScoreAsync(
        string name,
        string value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordEvaluationAsync(EvaluationResult result) =>
        RecordEvaluationAsync(result, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordEvaluationAsync(
        EvaluationResult result,
        LangfuseEvaluationScoreOptions? options,
        CancellationToken cancellationToken) =>
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
    public void SetPrompt(string name, int? version = null)
    {
    }

    /// <inheritdoc />
    public void SetPrompt(LangfusePrompt prompt)
    {
    }

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string name, double value) =>
        RecordSessionScoreAsync(name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(
        string name,
        double value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string name, bool value) =>
        RecordSessionScoreAsync(name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(
        string name,
        bool value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(string name, string value) =>
        RecordSessionScoreAsync(name, value, options: null, CancellationToken.None);

    /// <inheritdoc />
    public Task RecordSessionScoreAsync(
        string name,
        string value,
        LangfuseScoreOptions? options,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
