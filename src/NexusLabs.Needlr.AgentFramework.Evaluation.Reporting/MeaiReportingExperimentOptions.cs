using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Reporting;

/// <summary>
/// Configures MEAI Reporting lifecycle and evaluation behavior for experiment trials.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed record MeaiReportingExperimentOptions<TCase, TOutput>
{
    /// <summary>
    /// Gets the explicit response-cache reuse behavior.
    /// </summary>
    public required MeaiReportingResponseReuseMode ResponseReuseMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether Reporting publication failure is required for aggregate
    /// publication health.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets the behavior when the Reporting scenario cannot be created before task execution.
    /// </summary>
    public ExperimentItemScopeFailureMode FailureMode { get; init; } =
        ExperimentItemScopeFailureMode.ExecutionPrerequisite;

    /// <summary>
    /// Gets an optional factory for additional MEAI evaluation context derived from the terminal
    /// item output.
    /// </summary>
    public Func<
        ExperimentItemEvaluationContext<TCase, TOutput>,
        IEnumerable<EvaluationContext>?>? AdditionalContextFactory { get; init; }

    internal void Validate()
    {
        if (!Enum.IsDefined(ResponseReuseMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ResponseReuseMode),
                ResponseReuseMode,
                "The MEAI Reporting response reuse mode is not defined.");
        }

        if (!Enum.IsDefined(FailureMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(FailureMode),
                FailureMode,
                "The experiment item scope failure mode is not defined.");
        }
    }
}
