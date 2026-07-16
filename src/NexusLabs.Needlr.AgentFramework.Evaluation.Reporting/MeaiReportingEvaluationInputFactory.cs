using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Reporting;

/// <summary>
/// Projects one terminal successful experiment output into MEAI evaluation inputs.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
/// <param name="context">The terminal item evaluation context.</param>
/// <returns>The messages and model response evaluated by the reporting scenario.</returns>
public delegate EvaluationInputs MeaiReportingEvaluationInputFactory<TCase, TOutput>(
    ExperimentItemEvaluationContext<TCase, TOutput> context);
