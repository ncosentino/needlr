using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Configures Langfuse scenario traces and publication behavior for provider-neutral experiment
/// trials.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
public sealed record LangfuseExperimentItemScopeOptions<TCase>
{
    /// <summary>
    /// Gets a value indicating whether Langfuse publication failure is required for aggregate
    /// publication health.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>Gets the behavior when Langfuse scope entry or activation fails.</summary>
    public ExperimentItemScopeFailureMode FailureMode { get; init; } =
        ExperimentItemScopeFailureMode.BestEffort;

    /// <summary>
    /// Gets an optional factory for the stable trace name used by one statistical trial.
    /// </summary>
    /// <remarks>
    /// Prefer low-cardinality names. Put run, case, and trial identifiers in metadata rather than
    /// generating a distinct trace name for every item.
    /// </remarks>
    public Func<ExperimentItemScopeContext<TCase>, string?>? ScenarioNameFactory { get; init; }

    /// <summary>Gets optional tags applied to every item trace.</summary>
    public IEnumerable<string>? Tags { get; init; }

    /// <summary>Gets optional filterable metadata applied to every item trace.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    internal void Validate()
    {
        if (!Enum.IsDefined(FailureMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(FailureMode),
                FailureMode,
                "The experiment item scope failure mode is not defined.");
        }
    }
}
