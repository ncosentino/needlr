using System.Text.Json.Serialization;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes one isolated run-evaluation outcome.
/// </summary>
public sealed record ExperimentRunEvaluationResult
{
    private ExperimentRunEvaluationResult(
        string name,
        ExperimentRunEvaluationStatus status,
        EvaluationResult? evaluation,
        IReadOnlyList<ExperimentMetricSnapshot> metrics,
        ExperimentFailure? failure)
    {
        Name = name;
        Status = status;
        Evaluation = evaluation;
        Metrics = metrics;
        Failure = failure;
    }

    /// <summary>Gets the stable evaluator name.</summary>
    public string Name { get; }

    /// <summary>Gets the evaluator status.</summary>
    public ExperimentRunEvaluationStatus Status { get; }

    /// <summary>Gets the mutable MEAI result when evaluation succeeded.</summary>
    [JsonIgnore]
    public EvaluationResult? Evaluation { get; }

    /// <summary>Gets immutable normalized metric snapshots.</summary>
    public IReadOnlyList<ExperimentMetricSnapshot> Metrics { get; }

    /// <summary>Gets the structured failure when evaluation failed.</summary>
    public ExperimentFailure? Failure { get; }

    /// <summary>Creates a successful run-evaluation result.</summary>
    /// <param name="name">The stable evaluator name.</param>
    /// <param name="evaluation">The mutable MEAI evaluation result.</param>
    /// <returns>A successful result containing normalized metric snapshots.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="evaluation"/> is <see langword="null"/>.
    /// </exception>
    public static ExperimentRunEvaluationResult Succeeded(
        string name,
        EvaluationResult evaluation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(evaluation);
        return new ExperimentRunEvaluationResult(
            name,
            ExperimentRunEvaluationStatus.Succeeded,
            evaluation,
            ExperimentMetricSnapshotFactory.Create(evaluation),
            failure: null);
    }

    /// <summary>Creates a failed run-evaluation result.</summary>
    /// <param name="name">The stable evaluator name.</param>
    /// <param name="failure">The structured run-evaluation failure.</param>
    /// <returns>A failed result without evaluation metrics.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is blank or <paramref name="failure"/> is not a non-retryable
    /// run-evaluation failure.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="failure"/> is <see langword="null"/>.
    /// </exception>
    public static ExperimentRunEvaluationResult Failed(
        string name,
        ExperimentFailure failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(failure);
        if (failure.Code != ExperimentFailureCode.RunEvaluationFailed
            || failure.Stage != ExperimentFailureStage.RunEvaluation
            || failure.IsRetryable)
        {
            throw new ArgumentException(
                "A failed run evaluation must use the run-evaluation failure code and stage and cannot be retryable.",
                nameof(failure));
        }

        return new ExperimentRunEvaluationResult(
            name,
            ExperimentRunEvaluationStatus.Failed,
            evaluation: null,
            metrics: [],
            new ExperimentFailure(
                failure.Code,
                failure.Stage,
                failure.ExceptionType,
                failure.Message,
                isRetryable: false));
    }
}
