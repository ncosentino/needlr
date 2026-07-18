namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides the canonical ordered result for one provider-neutral experiment run.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed record ExperimentRunResult<TCase, TOutput>
{
    /// <summary>Gets the current canonical result schema version.</summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary>
    /// Initializes a validated canonical experiment run result.
    /// </summary>
    /// <param name="runId">The caller-supplied run identifier.</param>
    /// <param name="experimentName">The experiment name.</param>
    /// <param name="source">The materialized source identity.</param>
    /// <param name="startedAt">The UTC run start time.</param>
    /// <param name="duration">The elapsed run duration.</param>
    /// <param name="maxConcurrency">The configured maximum active attempt count.</param>
    /// <param name="workerCount">The fixed number of worker tasks used by the run.</param>
    /// <param name="items">The ordered item results.</param>
    /// <param name="runEvaluations">The ordered run-evaluation results.</param>
    /// <param name="policyResults">The ordered policy results.</param>
    /// <exception cref="ArgumentException">
    /// Identity, source, item ordering, item identity, run-evaluation names, or policy names are
    /// invalid or duplicated.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// A required value, collection, or collection element is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Time, duration, concurrency, or worker-count values are outside their supported range.
    /// </exception>
    public ExperimentRunResult(
        string runId,
        string experimentName,
        ExperimentSourceReference source,
        DateTimeOffset startedAt,
        TimeSpan duration,
        int maxConcurrency,
        int workerCount,
        IReadOnlyList<ExperimentItemResult<TCase, TOutput>> items,
        IReadOnlyList<ExperimentRunEvaluationResult> runEvaluations,
        IReadOnlyList<ExperimentPolicyResult> policyResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(experimentName);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Name);
        if (startedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The run start time must use a UTC (zero) offset.",
                nameof(startedAt));
        }

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "The run duration must be non-negative.");
        }

        if (maxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrency),
                maxConcurrency,
                "The maximum concurrency must be positive.");
        }

        var itemSnapshot = SnapshotItems(items);
        var maximumWorkerCount = Math.Min(maxConcurrency, itemSnapshot.Count);
        if (workerCount < 0
            || (itemSnapshot.Count == 0 && workerCount != 0)
            || (itemSnapshot.Count > 0
                && (workerCount < 1 || workerCount > maximumWorkerCount)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(workerCount),
                workerCount,
                "The worker count must match the bounded item population.");
        }

        var runEvaluationSnapshot = SnapshotRunEvaluations(runEvaluations);
        var policySnapshot = SnapshotPolicies(policyResults);
        SchemaVersion = CurrentSchemaVersion;
        RunId = runId;
        ExperimentName = experimentName;
        Source = new ExperimentSourceReference
        {
            Name = source.Name,
            Id = source.Id,
            Version = source.Version,
        };
        StartedAt = startedAt;
        Duration = duration;
        MaxConcurrency = maxConcurrency;
        WorkerCount = workerCount;
        Items = itemSnapshot;
        RunEvaluations = runEvaluationSnapshot;
        PolicyResults = policySnapshot;
        Decision = ReduceDecision(policySnapshot);
    }

    /// <summary>Gets the canonical result schema version.</summary>
    public int SchemaVersion { get; }

    /// <summary>Gets the caller-supplied run identifier.</summary>
    public string RunId { get; }

    /// <summary>Gets the experiment name.</summary>
    public string ExperimentName { get; }

    /// <summary>Gets the materialized source identity.</summary>
    public ExperimentSourceReference Source { get; }

    /// <summary>Gets the UTC run start time.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Gets the elapsed run duration.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Gets the configured maximum active attempt count.</summary>
    public int MaxConcurrency { get; }

    /// <summary>Gets the fixed number of worker tasks used by this run.</summary>
    public int WorkerCount { get; }

    /// <summary>Gets item results in stable source/trial sequence order.</summary>
    public IReadOnlyList<ExperimentItemResult<TCase, TOutput>> Items { get; }

    /// <summary>Gets isolated run-evaluation results in registration order.</summary>
    public IReadOnlyList<ExperimentRunEvaluationResult> RunEvaluations { get; }

    /// <summary>Gets isolated policy results in registration order.</summary>
    public IReadOnlyList<ExperimentPolicyResult> PolicyResults { get; }

    /// <summary>Gets the deterministic aggregate decision from required policies.</summary>
    public ExperimentRunDecision Decision { get; }

    private static IReadOnlyList<ExperimentItemResult<TCase, TOutput>> SnapshotItems(
        IReadOnlyList<ExperimentItemResult<TCase, TOutput>> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var identities = new HashSet<(string CaseId, int TrialIndex)>();
        var snapshot = new ExperimentItemResult<TCase, TOutput>[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            ArgumentNullException.ThrowIfNull(item);
            if (item.Sequence != index)
            {
                throw new ArgumentException(
                    "Item sequences must be contiguous, zero-based, and ordered.",
                    nameof(items));
            }

            if (!identities.Add((item.Case.Id, item.TrialIndex)))
            {
                throw new ArgumentException(
                    $"Case '{item.Case.Id}' trial '{item.TrialIndex}' appears more than once.",
                    nameof(items));
            }

            snapshot[index] = item;
        }

        return Array.AsReadOnly(snapshot);
    }

    private static IReadOnlyList<ExperimentRunEvaluationResult> SnapshotRunEvaluations(
        IReadOnlyList<ExperimentRunEvaluationResult> runEvaluations)
    {
        ArgumentNullException.ThrowIfNull(runEvaluations);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var snapshot = new ExperimentRunEvaluationResult[runEvaluations.Count];
        for (var index = 0; index < runEvaluations.Count; index++)
        {
            var evaluation = runEvaluations[index];
            ArgumentNullException.ThrowIfNull(evaluation);
            if (!names.Add(evaluation.Name))
            {
                throw new ArgumentException(
                    $"Run-evaluation name '{evaluation.Name}' appears more than once.",
                    nameof(runEvaluations));
            }

            snapshot[index] = evaluation;
        }

        return Array.AsReadOnly(snapshot);
    }

    private static IReadOnlyList<ExperimentPolicyResult> SnapshotPolicies(
        IReadOnlyList<ExperimentPolicyResult> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var snapshot = new ExperimentPolicyResult[policies.Count];
        for (var index = 0; index < policies.Count; index++)
        {
            var policy = policies[index];
            ArgumentNullException.ThrowIfNull(policy);
            if (!names.Add(policy.Name))
            {
                throw new ArgumentException(
                    $"Policy name '{policy.Name}' appears more than once.",
                    nameof(policies));
            }

            snapshot[index] = policy;
        }

        return Array.AsReadOnly(snapshot);
    }

    private static ExperimentRunDecision ReduceDecision(
        IReadOnlyList<ExperimentPolicyResult> policies)
    {
        var required = policies.Where(policy => policy.IsRequired).ToArray();
        if (required.Length == 0)
        {
            return ExperimentRunDecision.NotEvaluated;
        }

        if (required.Any(policy => policy.Decision == EvaluationDecision.Failed))
        {
            return ExperimentRunDecision.Failed;
        }

        if (required.Any(policy =>
                policy.Decision == EvaluationDecision.Inconclusive))
        {
            return ExperimentRunDecision.Inconclusive;
        }

        return ExperimentRunDecision.Passed;
    }
}
