using Microsoft.Extensions.AI.Evaluation.Reporting;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Reporting;

/// <summary>
/// Adds coordinated MEAI Reporting lifecycle and evaluation to provider-neutral experiments.
/// </summary>
public static class MeaiReportingExperimentExtensions
{
    /// <summary>
    /// Adds one MEAI Reporting scenario per statistical trial and occupies the definition's single
    /// item-evaluator slot.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="definition">The experiment definition to copy and configure.</param>
    /// <param name="configuration">
    /// The Reporting configuration whose execution name must match the Needlr run ID.
    /// </param>
    /// <param name="inputFactory">
    /// The projection from a terminal successful output to MEAI messages and model response.
    /// </param>
    /// <param name="options">The required response reuse and publication behavior.</param>
    /// <returns>
    /// A copy of <paramref name="definition"/> with the coordinated Reporting scope and evaluator.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="definition"/>, <paramref name="configuration"/>,
    /// <paramref name="inputFactory"/>, or <paramref name="options"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The Reporting configuration is inconsistent with the selected response reuse mode.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The definition already has an item evaluator or MEAI Reporting item scope.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The configured response reuse or item-scope failure mode is undefined.
    /// </exception>
    public static ExperimentDefinition<TCase, TOutput> WithMeaiReporting<TCase, TOutput>(
            this ExperimentDefinition<TCase, TOutput> definition,
            ReportingConfiguration configuration,
            MeaiReportingEvaluationInputFactory<TCase, TOutput> inputFactory,
            MeaiReportingExperimentOptions<TCase, TOutput> options)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.ItemScopes);
        if (definition.ItemEvaluator is not null)
        {
            throw new InvalidOperationException(
                "MEAI Reporting cannot be added to an experiment definition that already has an " +
                "item evaluator. Configure every MEAI evaluator on the ReportingConfiguration.");
        }

        if (definition.ItemScopes.Any(scope =>
                string.Equals(
                    scope.Name,
                    MeaiReportingExperimentSchema.ProviderName,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "The experiment definition already contains a MEAI Reporting item scope.");
        }

        var adapter = CreateExperimentAdapter(
            configuration,
            inputFactory,
            options);
        return definition with
        {
            ItemScopes = [.. definition.ItemScopes, adapter],
            ItemEvaluator = adapter.EvaluateAsync,
        };
    }

    internal static MeaiReportingExperimentAdapter<TCase, TOutput>
        CreateExperimentAdapter<TCase, TOutput>(
            this ReportingConfiguration configuration,
            MeaiReportingEvaluationInputFactory<TCase, TOutput> inputFactory,
            MeaiReportingExperimentOptions<TCase, TOutput> options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(inputFactory);
        ArgumentNullException.ThrowIfNull(options);
        return new MeaiReportingExperimentAdapter<TCase, TOutput>(
            configuration,
            inputFactory,
            options);
    }
}
