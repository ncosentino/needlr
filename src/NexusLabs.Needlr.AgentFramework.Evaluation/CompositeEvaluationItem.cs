using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// A single evaluation outcome within a <see cref="CompositeEvaluationResult"/>, pairing the
/// evaluator inputs that were scored with the <see cref="EvaluationResult"/> produced for them.
/// </summary>
/// <param name="Label">
/// A human-readable label identifying the subject of the evaluation. For single-run helpers this
/// is typically the evaluator's name; for multi-stage pipelines this is the agent stage name.
/// </param>
/// <param name="Inputs">
/// The <see cref="EvaluationInputs"/> that were handed to the evaluator. Preserved so consumers
/// can correlate scores back to the exact conversation slice that produced them.
/// </param>
/// <param name="Result">
/// The <see cref="EvaluationResult"/> returned by the evaluator, containing one or more metrics.
/// </param>
public readonly record struct CompositeEvaluationItem(
    string Label,
    EvaluationInputs Inputs,
    EvaluationResult Result);
