using System.Text.Json.Serialization;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes one case trial, including its complete attempt history and terminal output.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed record ExperimentItemResult<TCase, TOutput>
{
    private ExperimentItemResult(
        int sequence,
        ExperimentCase<TCase> @case,
        int trialIndex,
        ExperimentItemStatus status,
        IReadOnlyList<ExperimentAttemptResult> attempts,
        bool hasOutput,
        TOutput? output,
        EvaluationResult? evaluation,
        IReadOnlyList<ExperimentMetricSnapshot> metrics,
        IReadOnlyList<ExperimentItemCorrelation> correlations,
        IReadOnlyList<ExperimentItemPublicationResult> publications,
        ExperimentFailure? failure)
    {
        Sequence = sequence;
        Case = @case;
        TrialIndex = trialIndex;
        Status = status;
        Attempts = attempts;
        HasOutput = hasOutput;
        Output = output;
        Evaluation = evaluation;
        Metrics = metrics;
        Correlations = correlations;
        Publications = publications;
        Failure = failure;
    }

    /// <summary>Gets the zero-based stable sequence.</summary>
    public int Sequence { get; }

    /// <summary>Gets the materialized case.</summary>
    public ExperimentCase<TCase> Case { get; }

    /// <summary>Gets the one-based statistical trial index.</summary>
    public int TrialIndex { get; }

    /// <summary>Gets the terminal item status.</summary>
    public ExperimentItemStatus Status { get; }

    /// <summary>Gets every operational attempt in order.</summary>
    public IReadOnlyList<ExperimentAttemptResult> Attempts { get; }

    /// <summary>Gets a value indicating whether <see cref="Output"/> contains a task output.</summary>
    public bool HasOutput { get; }

    /// <summary>Gets the terminal successful output, when available.</summary>
    public TOutput? Output { get; }

    /// <summary>Gets the mutable MEAI evaluation result, when evaluation succeeded.</summary>
    [JsonIgnore]
    public EvaluationResult? Evaluation { get; }

    /// <summary>Gets immutable normalized metric snapshots.</summary>
    public IReadOnlyList<ExperimentMetricSnapshot> Metrics { get; }

    /// <summary>Gets namespaced provider identifiers in item-scope registration order.</summary>
    public IReadOnlyList<ExperimentItemCorrelation> Correlations { get; }

    /// <summary>Gets item-scope publication results in registration order.</summary>
    public IReadOnlyList<ExperimentItemPublicationResult> Publications { get; }

    /// <summary>Gets the structured terminal failure, when present.</summary>
    public ExperimentFailure? Failure { get; }

    /// <summary>Creates a successful item result, including a possibly null task output.</summary>
    /// <remarks>
    /// The final attempt must be successful. Needlr-owned collections and case tags are snapshotted.
    /// </remarks>
    /// <param name="sequence">The zero-based stable item sequence.</param>
    /// <param name="case">The materialized experiment case.</param>
    /// <param name="trialIndex">The one-based statistical trial index.</param>
    /// <param name="attempts">The complete ordered attempt history.</param>
    /// <param name="output">The task output, which may be null.</param>
    /// <param name="evaluation">The optional successful MEAI evaluation result.</param>
    /// <param name="publications">The item-scope publication results.</param>
    /// <returns>A successful canonical item result.</returns>
    /// <exception cref="ArgumentException">
    /// Case identity, tags, attempt numbering, final attempt status, or publication identity is
    /// invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="case"/>, <paramref name="attempts"/>, <paramref name="publications"/>, or an
    /// owned collection element is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sequence"/> or <paramref name="trialIndex"/> is outside its supported range.
    /// </exception>
    public static ExperimentItemResult<TCase, TOutput> Succeeded(
        int sequence,
        ExperimentCase<TCase> @case,
        int trialIndex,
        IReadOnlyList<ExperimentAttemptResult> attempts,
        TOutput? output,
        EvaluationResult? evaluation,
        IReadOnlyList<ExperimentItemPublicationResult> publications)
    {
        var identity = ValidateIdentity(sequence, @case, trialIndex);
        var attemptSnapshot = SnapshotAttempts(attempts);
        RequireFinalSuccessfulAttempt(attemptSnapshot);
        var publicationSnapshot = SnapshotPublications(publications);
        var metrics = evaluation is null
            ? []
            : ExperimentMetricSnapshotFactory.Create(evaluation);
        return Create(
            sequence,
            identity,
            trialIndex,
            ExperimentItemStatus.Succeeded,
            attemptSnapshot,
            hasOutput: true,
            output,
            evaluation,
            metrics,
            publicationSnapshot,
            failure: null);
    }

    /// <summary>Creates an item result whose task succeeded but item evaluation failed.</summary>
    /// <remarks>
    /// The final attempt must be successful and the failure must use the item-evaluation failure
    /// code and stage.
    /// </remarks>
    /// <param name="sequence">The zero-based stable item sequence.</param>
    /// <param name="case">The materialized experiment case.</param>
    /// <param name="trialIndex">The one-based statistical trial index.</param>
    /// <param name="attempts">The complete ordered attempt history.</param>
    /// <param name="output">The successful task output, which may be null.</param>
    /// <param name="failure">The structured item-evaluation failure.</param>
    /// <param name="publications">The item-scope publication results.</param>
    /// <returns>A canonical evaluation-failed item result.</returns>
    /// <exception cref="ArgumentException">
    /// Identity, attempts, publication identity, or failure shape is invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="case"/>, <paramref name="attempts"/>, <paramref name="failure"/>,
    /// <paramref name="publications"/>, or an owned collection element is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sequence"/> or <paramref name="trialIndex"/> is outside its supported range.
    /// </exception>
    public static ExperimentItemResult<TCase, TOutput> EvaluationFailed(
        int sequence,
        ExperimentCase<TCase> @case,
        int trialIndex,
        IReadOnlyList<ExperimentAttemptResult> attempts,
        TOutput? output,
        ExperimentFailure failure,
        IReadOnlyList<ExperimentItemPublicationResult> publications)
    {
        var identity = ValidateIdentity(sequence, @case, trialIndex);
        var attemptSnapshot = SnapshotAttempts(attempts);
        RequireFinalSuccessfulAttempt(attemptSnapshot);
        var failureSnapshot = ValidateFailure(
            failure,
            ExperimentFailureCode.EvaluationFailed,
            ExperimentFailureStage.ItemEvaluation);
        return Create(
            sequence,
            identity,
            trialIndex,
            ExperimentItemStatus.EvaluationFailed,
            attemptSnapshot,
            hasOutput: true,
            output,
            evaluation: null,
            metrics: [],
            SnapshotPublications(publications),
            failureSnapshot);
    }

    /// <summary>Creates an item result for execution, cancellation, timeout, or prerequisite failure.</summary>
    /// <remarks>
    /// The item status, final attempt status, failure code, and failure stage must describe the
    /// same terminal condition. Retry-policy failures are represented as execution-failed items
    /// with a policy-stage failure.
    /// </remarks>
    /// <param name="sequence">The zero-based stable item sequence.</param>
    /// <param name="case">The materialized experiment case.</param>
    /// <param name="trialIndex">The one-based statistical trial index.</param>
    /// <param name="status">The terminal failed-item status.</param>
    /// <param name="attempts">The complete ordered attempt history.</param>
    /// <param name="failure">The structured terminal failure.</param>
    /// <param name="publications">The item-scope publication results.</param>
    /// <returns>A canonical failed item result.</returns>
    /// <exception cref="ArgumentException">
    /// Identity, item status, attempts, publication identity, or failure shape is invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="case"/>, <paramref name="attempts"/>, <paramref name="failure"/>,
    /// <paramref name="publications"/>, or an owned collection element is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sequence"/> or <paramref name="trialIndex"/> is outside its supported range.
    /// </exception>
    public static ExperimentItemResult<TCase, TOutput> Failed(
        int sequence,
        ExperimentCase<TCase> @case,
        int trialIndex,
        ExperimentItemStatus status,
        IReadOnlyList<ExperimentAttemptResult> attempts,
        ExperimentFailure failure,
        IReadOnlyList<ExperimentItemPublicationResult> publications)
    {
        var identity = ValidateIdentity(sequence, @case, trialIndex);
        var attemptSnapshot = SnapshotAttempts(attempts);
        ArgumentNullException.ThrowIfNull(failure);
        var expectedAttemptStatus = status switch
        {
            ExperimentItemStatus.ExecutionFailed => ExperimentAttemptStatus.Failed,
            ExperimentItemStatus.TimedOut => ExperimentAttemptStatus.TimedOut,
            ExperimentItemStatus.Canceled => ExperimentAttemptStatus.Canceled,
            ExperimentItemStatus.PrerequisiteFailed => (ExperimentAttemptStatus?)null,
            _ => throw new ArgumentException(
                $"Item status '{status}' is not a supported failed-item status.",
                nameof(status)),
        };
        if (expectedAttemptStatus is { } attemptStatus)
        {
            if (attemptSnapshot.Count == 0
                || attemptSnapshot[^1].Status != attemptStatus)
            {
                throw new ArgumentException(
                    $"Item status '{status}' requires a final '{attemptStatus}' attempt.",
                    nameof(attempts));
            }
        }

        var failureSnapshot = status switch
        {
            ExperimentItemStatus.ExecutionFailed
                when failure.Code == ExperimentFailureCode.RetryPolicyFailed =>
                ValidateFailure(
                    failure,
                    ExperimentFailureCode.RetryPolicyFailed,
                    ExperimentFailureStage.Policy),
            ExperimentItemStatus.ExecutionFailed => ValidateFailure(
                failure,
                ExperimentFailureCode.ExecutionFailed,
                ExperimentFailureStage.Execution),
            ExperimentItemStatus.TimedOut => ValidateFailure(
                failure,
                ExperimentFailureCode.AttemptTimedOut,
                ExperimentFailureStage.Execution),
            ExperimentItemStatus.Canceled => ValidateFailure(
                failure,
                ExperimentFailureCode.TaskCanceled,
                ExperimentFailureStage.Execution),
            ExperimentItemStatus.PrerequisiteFailed => ValidateFailure(
                failure,
                ExperimentFailureCode.ItemScopePrerequisiteFailed,
                ExperimentFailureStage.Publication),
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The failed item status is not defined."),
        };
        return Create(
            sequence,
            identity,
            trialIndex,
            status,
            attemptSnapshot,
            hasOutput: false,
            output: default,
            evaluation: null,
            metrics: [],
            SnapshotPublications(publications),
            failureSnapshot);
    }

    internal ExperimentItemResult<TCase, TOutput> WithPublications(
        IReadOnlyList<ExperimentItemPublicationResult> publications) =>
        Create(
            Sequence,
            Case,
            TrialIndex,
            Status,
            Attempts,
            HasOutput,
            Output,
            Evaluation,
            Metrics,
            SnapshotPublications(publications),
            Failure);

    private static ExperimentItemResult<TCase, TOutput> Create(
        int sequence,
        ExperimentCase<TCase> @case,
        int trialIndex,
        ExperimentItemStatus status,
        IReadOnlyList<ExperimentAttemptResult> attempts,
        bool hasOutput,
        TOutput? output,
        EvaluationResult? evaluation,
        IReadOnlyList<ExperimentMetricSnapshot> metrics,
        IReadOnlyList<ExperimentItemPublicationResult> publications,
        ExperimentFailure? failure)
    {
        var correlations = Array.AsReadOnly(publications
            .SelectMany(publication => publication.Correlations)
            .ToArray());
        return new ExperimentItemResult<TCase, TOutput>(
            sequence,
            @case,
            trialIndex,
            status,
            attempts,
            hasOutput,
            output,
            evaluation,
            Array.AsReadOnly(metrics.ToArray()),
            correlations,
            publications,
            failure);
    }

    private static ExperimentCase<TCase> ValidateIdentity(
        int sequence,
        ExperimentCase<TCase> @case,
        int trialIndex)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                sequence,
                "The item sequence must be non-negative.");
        }

        ArgumentNullException.ThrowIfNull(@case);
        ArgumentException.ThrowIfNullOrWhiteSpace(@case.Id);
        ArgumentNullException.ThrowIfNull(@case.Tags);
        if (@case.TrialCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(@case),
                @case.TrialCount,
                "The case trial count must be positive.");
        }

        if (trialIndex < 1 || trialIndex > @case.TrialCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trialIndex),
                trialIndex,
                "The trial index must be within the case trial count.");
        }

        var tags = new string[@case.Tags.Count];
        for (var index = 0; index < @case.Tags.Count; index++)
        {
            var tag = @case.Tags[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(tag);
            tags[index] = tag;
        }

        return new ExperimentCase<TCase>
        {
            Id = @case.Id,
            Value = @case.Value,
            TrialCount = @case.TrialCount,
            Tags = Array.AsReadOnly(tags),
        };
    }

    private static IReadOnlyList<ExperimentAttemptResult> SnapshotAttempts(
        IReadOnlyList<ExperimentAttemptResult> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        var snapshot = new ExperimentAttemptResult[attempts.Count];
        for (var index = 0; index < attempts.Count; index++)
        {
            var attempt = attempts[index];
            ArgumentNullException.ThrowIfNull(attempt);
            if (attempt.AttemptNumber != index + 1)
            {
                throw new ArgumentException(
                    "Attempt numbers must be contiguous and one-based.",
                    nameof(attempts));
            }

            snapshot[index] = attempt;
        }

        return Array.AsReadOnly(snapshot);
    }

    private static IReadOnlyList<ExperimentItemPublicationResult> SnapshotPublications(
        IReadOnlyList<ExperimentItemPublicationResult> publications)
    {
        ArgumentNullException.ThrowIfNull(publications);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var snapshot = new ExperimentItemPublicationResult[publications.Count];
        for (var index = 0; index < publications.Count; index++)
        {
            var publication = publications[index];
            ArgumentNullException.ThrowIfNull(publication);
            if (!names.Add(publication.Name))
            {
                throw new ArgumentException(
                    $"Item publication name '{publication.Name}' appears more than once.",
                    nameof(publications));
            }

            snapshot[index] = publication;
        }

        return Array.AsReadOnly(snapshot);
    }

    private static void RequireFinalSuccessfulAttempt(
        IReadOnlyList<ExperimentAttemptResult> attempts)
    {
        if (attempts.Count == 0
            || attempts[^1].Status != ExperimentAttemptStatus.Succeeded)
        {
            throw new ArgumentException(
                "A successful task result requires a final successful attempt.",
                nameof(attempts));
        }
    }

    private static ExperimentFailure ValidateFailure(
        ExperimentFailure failure,
        ExperimentFailureCode expectedCode,
        ExperimentFailureStage expectedStage)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (failure.Code != expectedCode
            || failure.Stage != expectedStage
            || failure.IsRetryable)
        {
            throw new ArgumentException(
                $"The item failure must use code '{expectedCode}', stage '{expectedStage}', and cannot be retryable.",
                nameof(failure));
        }

        return new ExperimentFailure(
            failure.Code,
            failure.Stage,
            failure.ExceptionType,
            failure.Message,
            isRetryable: false);
    }
}
