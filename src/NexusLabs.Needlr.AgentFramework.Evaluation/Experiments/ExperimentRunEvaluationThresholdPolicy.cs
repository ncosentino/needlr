namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Applies reusable deterministic thresholds to one named run evaluation.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed class ExperimentRunEvaluationThresholdPolicy<TCase, TOutput> :
    IExperimentRunPolicy<TCase, TOutput>
{
    private readonly EvaluationThresholdEvaluator _thresholds;
    private readonly EvaluationMissingMetricBehavior _missingMetricBehavior;

    /// <summary>
    /// Initializes a deterministic run-evaluation threshold policy.
    /// </summary>
    /// <param name="name">The stable policy name.</param>
    /// <param name="runEvaluationName">The run evaluator that supplies metrics.</param>
    /// <param name="thresholds">The configured reusable threshold evaluator.</param>
    /// <param name="isRequired">Whether this policy contributes to the run decision.</param>
    /// <param name="missingMetricBehavior">
    /// The treatment for unavailable, missing, or invalid required evidence.
    /// </param>
    public ExperimentRunEvaluationThresholdPolicy(
        string name,
        string runEvaluationName,
        EvaluationThresholdEvaluator thresholds,
        bool isRequired = true,
        EvaluationMissingMetricBehavior missingMetricBehavior =
            EvaluationMissingMetricBehavior.Inconclusive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(runEvaluationName);
        ArgumentNullException.ThrowIfNull(thresholds);
        if (!Enum.IsDefined(missingMetricBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(missingMetricBehavior),
                missingMetricBehavior,
                "The missing metric behavior is not defined.");
        }

        Name = name;
        RunEvaluationName = runEvaluationName;
        IsRequired = isRequired;
        _thresholds = thresholds;
        _missingMetricBehavior = missingMetricBehavior;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>Gets the run evaluator that supplies metrics.</summary>
    public string RunEvaluationName { get; }

    /// <inheritdoc />
    public ExperimentPolicyKind Kind => ExperimentPolicyKind.Deterministic;

    /// <inheritdoc />
    public bool IsRequired { get; }

    /// <inheritdoc />
    public ValueTask<ExperimentPolicyVerdict> EvaluateAsync(
        ExperimentPolicyContext<TCase, TOutput> context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var runEvaluation = context.RunEvaluations.FirstOrDefault(
            evaluation => string.Equals(
                evaluation.Name,
                RunEvaluationName,
                StringComparison.Ordinal));
        if (runEvaluation is null
            || runEvaluation.Status != ExperimentRunEvaluationStatus.Succeeded)
        {
            var unavailableReason = runEvaluation is null
                ? $"Run evaluation '{RunEvaluationName}' was not configured."
                : $"Run evaluation '{RunEvaluationName}' did not complete successfully.";
            return ValueTask.FromResult(new ExperimentPolicyVerdict
            {
                Decision = _missingMetricBehavior == EvaluationMissingMetricBehavior.Fail
                    ? EvaluationDecision.Failed
                    : EvaluationDecision.Inconclusive,
                DeterministicEvidence = new ExperimentDeterministicPolicyEvidence
                {
                    RunEvaluationName = RunEvaluationName,
                    UnavailableReason = unavailableReason,
                },
            });
        }

        var thresholdResult = _thresholds.Evaluate(
            runEvaluation.Metrics,
            _missingMetricBehavior);
        return ValueTask.FromResult(new ExperimentPolicyVerdict
        {
            Decision = thresholdResult.Decision,
            DeterministicEvidence = new ExperimentDeterministicPolicyEvidence
            {
                RunEvaluationName = RunEvaluationName,
                Thresholds = thresholdResult,
            },
        });
    }
}
