namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes one isolated experiment policy outcome.
/// </summary>
public sealed record ExperimentPolicyResult
{
    private ExperimentPolicyResult(
        string name,
        ExperimentPolicyKind kind,
        bool isRequired,
        EvaluationDecision decision,
        ExperimentDeterministicPolicyEvidence? deterministicEvidence,
        ExperimentBinaryStatisticalEvidence? statisticalEvidence,
        ExperimentFailure? failure)
    {
        Name = name;
        Kind = kind;
        IsRequired = isRequired;
        Decision = decision;
        DeterministicEvidence = deterministicEvidence;
        StatisticalEvidence = statisticalEvidence;
        Failure = failure;
    }

    /// <summary>Gets the stable policy name.</summary>
    public string Name { get; }

    /// <summary>Gets the policy kind.</summary>
    public ExperimentPolicyKind Kind { get; }

    /// <summary>Gets a value indicating whether the policy contributes to the run decision.</summary>
    public bool IsRequired { get; }

    /// <summary>Gets the policy decision.</summary>
    public EvaluationDecision Decision { get; }

    /// <summary>Gets deterministic threshold evidence, when applicable.</summary>
    public ExperimentDeterministicPolicyEvidence? DeterministicEvidence { get; }

    /// <summary>Gets binary statistical evidence, when applicable.</summary>
    public ExperimentBinaryStatisticalEvidence? StatisticalEvidence { get; }

    /// <summary>Gets the structured failure when policy execution failed.</summary>
    public ExperimentFailure? Failure { get; }

    /// <summary>Creates a canonical policy result from a successful policy verdict.</summary>
    /// <param name="name">The stable policy name.</param>
    /// <param name="kind">The policy kind.</param>
    /// <param name="isRequired">Whether the policy contributes to the run decision.</param>
    /// <param name="verdict">The validated policy verdict.</param>
    /// <returns>A canonical successful policy result.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is blank or <paramref name="verdict"/> contains evidence that does
    /// not match <paramref name="kind"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="verdict"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind"/> is undefined.
    /// </exception>
    public static ExperimentPolicyResult FromVerdict(
        string name,
        ExperimentPolicyKind kind,
        bool isRequired,
        ExperimentPolicyVerdict verdict)
    {
        ValidateIdentity(name, kind);
        ArgumentNullException.ThrowIfNull(verdict);
        if (kind == ExperimentPolicyKind.Deterministic
            && verdict.StatisticalEvidence is not null)
        {
            throw new ArgumentException(
                "A deterministic policy cannot return statistical evidence.",
                nameof(verdict));
        }

        if (kind == ExperimentPolicyKind.Statistical
            && verdict.DeterministicEvidence is not null)
        {
            throw new ArgumentException(
                "A statistical policy cannot return deterministic evidence.",
                nameof(verdict));
        }

        return new ExperimentPolicyResult(
            name,
            kind,
            isRequired,
            verdict.Decision,
            verdict.DeterministicEvidence,
            verdict.StatisticalEvidence,
            failure: null);
    }

    /// <summary>Creates a canonical result for a policy execution failure.</summary>
    /// <param name="name">The stable policy name.</param>
    /// <param name="kind">The policy kind.</param>
    /// <param name="isRequired">Whether the policy contributes to the run decision.</param>
    /// <param name="failure">The structured policy execution failure.</param>
    /// <returns>An inconclusive policy result containing the failure.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is blank or <paramref name="failure"/> is not a non-retryable policy
    /// execution failure.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="failure"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind"/> is undefined.
    /// </exception>
    public static ExperimentPolicyResult ExecutionFailed(
        string name,
        ExperimentPolicyKind kind,
        bool isRequired,
        ExperimentFailure failure)
    {
        ValidateIdentity(name, kind);
        ArgumentNullException.ThrowIfNull(failure);
        if (failure.Code != ExperimentFailureCode.PolicyFailed
            || failure.Stage != ExperimentFailureStage.Policy
            || failure.IsRetryable)
        {
            throw new ArgumentException(
                "A policy execution failure must use the policy failure code and stage and cannot be retryable.",
                nameof(failure));
        }

        return new ExperimentPolicyResult(
            name,
            kind,
            isRequired,
            EvaluationDecision.Inconclusive,
            deterministicEvidence: null,
            statisticalEvidence: null,
            new ExperimentFailure(
                failure.Code,
                failure.Stage,
                failure.ExceptionType,
                failure.Message,
                isRetryable: false));
    }

    private static void ValidateIdentity(string name, ExperimentPolicyKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "The policy kind is not defined.");
        }
    }
}
