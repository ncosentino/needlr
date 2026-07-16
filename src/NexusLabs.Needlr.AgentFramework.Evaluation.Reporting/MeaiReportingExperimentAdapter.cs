using System.Globalization;

using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Reporting;

/// <summary>
/// Coordinates one MEAI Reporting scenario and the single item evaluator for each experiment trial.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
[DoNotAutoRegister]
public sealed class MeaiReportingExperimentAdapter<TCase, TOutput> :
    IExperimentItemScopeProvider<TCase, TOutput>
{
    /// <summary>Gets the provider name used in item publication results.</summary>
    public const string ProviderName = "meai-reporting";

    /// <summary>Gets the namespace used for MEAI Reporting item correlations.</summary>
    public const string CorrelationNamespace = "meai.reporting";

    /// <summary>Gets the execution-name correlation name.</summary>
    public const string ExecutionNameCorrelationName = "execution.name";

    /// <summary>Gets the scenario-name correlation name.</summary>
    public const string ScenarioNameCorrelationName = "scenario.name";

    /// <summary>Gets the iteration-name correlation name.</summary>
    public const string IterationNameCorrelationName = "iteration.name";

    private readonly ReportingConfiguration _configuration;
    private readonly MeaiReportingEvaluationInputFactory<TCase, TOutput> _inputFactory;
    private readonly Func<
        ExperimentItemEvaluationContext<TCase, TOutput>,
        IEnumerable<EvaluationContext>?>? _additionalContextFactory;
    private readonly MeaiReportingResponseReuseMode _responseReuseMode;

    internal MeaiReportingExperimentAdapter(
        ReportingConfiguration configuration,
        MeaiReportingEvaluationInputFactory<TCase, TOutput> inputFactory,
        MeaiReportingExperimentAdapterOptions<TCase, TOutput> options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(inputFactory);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        ValidateConfiguration(configuration, options.ResponseReuseMode);

        _configuration = configuration;
        _inputFactory = inputFactory;
        _additionalContextFactory = options.AdditionalContextFactory;
        _responseReuseMode = options.ResponseReuseMode;
        IsRequired = options.IsRequired;
        FailureMode = options.FailureMode;
    }

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public bool IsRequired { get; }

    /// <inheritdoc />
    public ExperimentItemScopeFailureMode FailureMode { get; }

    /// <inheritdoc />
    public async ValueTask<IExperimentItemScope<TCase, TOutput>> EnterAsync(
        ExperimentItemScopeContext<TCase> context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(
                _configuration.ExecutionName,
                context.RunId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The MEAI Reporting execution name '{_configuration.ExecutionName}' must match " +
                $"the Needlr run ID '{context.RunId}'.");
        }

        var iterationName = context.TrialIndex.ToString(CultureInfo.InvariantCulture);
        IEnumerable<string>? additionalCachingKeys =
            _responseReuseMode == MeaiReportingResponseReuseMode.FreshPerRun
                ? [context.RunId]
                : null;
        IEnumerable<string>? additionalTags =
            context.Case.Tags.Count == 0 ? null : context.Case.Tags;
        var scenarioRun = await _configuration
            .CreateScenarioRunAsync(
                context.Case.Id,
                iterationName,
                additionalCachingKeys,
                additionalTags,
                cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var item = new MeaiReportingExperimentItem(scenarioRun);
        return new MeaiReportingExperimentItemScope<TCase, TOutput>(
            item,
            IsRequired);
    }

    /// <summary>
    /// Evaluates one terminal successful output through the trial's MEAI Reporting scenario.
    /// </summary>
    /// <param name="context">The terminal item evaluation context.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The canonical MEAI evaluation result produced by the configured evaluators.</returns>
    public async ValueTask<EvaluationResult> EvaluateAsync(
        ExperimentItemEvaluationContext<TCase, TOutput> context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var item = context.Features.GetRequired<MeaiReportingExperimentItem>();
        var inputs = _inputFactory(context);
        var additionalContext = _additionalContextFactory?
            .Invoke(context)?
            .ToArray();
        return await item
            .EvaluateAsync(inputs, additionalContext, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ValidateConfiguration(
        ReportingConfiguration configuration,
        MeaiReportingResponseReuseMode responseReuseMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.ExecutionName);
        if (responseReuseMode == MeaiReportingResponseReuseMode.Disabled)
        {
            if (configuration.ResponseCacheProvider is not null)
            {
                throw new ArgumentException(
                    "The ReportingConfiguration must not contain a response cache provider when " +
                    "MEAI Reporting response reuse is disabled.",
                    nameof(configuration));
            }

            return;
        }

        if (configuration.ChatConfiguration is null
            || configuration.ResponseCacheProvider is null)
        {
            throw new ArgumentException(
                "The ReportingConfiguration must contain a chat configuration and response cache " +
                "provider when MEAI Reporting response reuse is enabled.",
                nameof(configuration));
        }
    }
}
