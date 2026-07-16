using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Reporting;

/// <summary>
/// Exposes one trial's MEAI Reporting identity and wrapped chat configuration to experiment work.
/// </summary>
/// <remarks>
/// Send model requests through <see cref="ChatConfiguration"/> when response caching is enabled.
/// The adapter retains the underlying <see cref="ScenarioRun"/> and invokes its evaluator exactly
/// once through the paired item evaluator.
/// </remarks>
[DoNotAutoRegister]
public sealed class MeaiReportingExperimentItem
{
    private readonly ScenarioRun _scenarioRun;
    private bool _evaluationStarted;

    internal MeaiReportingExperimentItem(ScenarioRun scenarioRun)
    {
        ArgumentNullException.ThrowIfNull(scenarioRun);
        _scenarioRun = scenarioRun;
    }

    /// <summary>Gets the MEAI execution name mapped from the Needlr run ID.</summary>
    public string ExecutionName => _scenarioRun.ExecutionName;

    /// <summary>Gets the MEAI scenario name mapped from the Needlr case ID.</summary>
    public string ScenarioName => _scenarioRun.ScenarioName;

    /// <summary>Gets the MEAI iteration name mapped from the one-based trial index.</summary>
    public string IterationName => _scenarioRun.IterationName;

    /// <summary>
    /// Gets the scenario-specific chat configuration that records and optionally caches responses.
    /// </summary>
    public ChatConfiguration? ChatConfiguration => _scenarioRun.ChatConfiguration;

    internal bool EvaluationCompleted { get; private set; }

    internal async ValueTask<EvaluationResult> EvaluateAsync(
        EvaluationInputs inputs,
        IEnumerable<EvaluationContext>? additionalContext,
        CancellationToken cancellationToken)
    {
        if (_evaluationStarted)
        {
            throw new InvalidOperationException(
                "A MEAI Reporting experiment item can be evaluated only once.");
        }

        ArgumentNullException.ThrowIfNull(inputs.Messages);
        ArgumentNullException.ThrowIfNull(inputs.ModelResponse);
        _evaluationStarted = true;

        var evaluation = await _scenarioRun
            .EvaluateAsync(
                inputs.Messages,
                inputs.ModelResponse,
                additionalContext,
                cancellationToken)
            .ConfigureAwait(false);
        EvaluationCompleted = true;
        return evaluation;
    }

    internal ValueTask PersistAsync() => _scenarioRun.DisposeAsync();
}
