using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Inputs shaped for <c>Microsoft.Extensions.AI.Evaluation</c> evaluators, derived from a
/// captured agent run. Consumers pass <see cref="Messages"/> and <see cref="ModelResponse"/>
/// to <c>IEvaluator.EvaluateAsync</c> (or to a <c>ScenarioRun</c> obtained via
/// <c>ReportingConfiguration.CreateScenarioRunAsync</c>).
/// </summary>
/// <param name="Messages">
/// The conversation messages that were sent to the agent, exactly as captured at the
/// agent-run boundary. Safe to replay without re-invoking the model.
/// </param>
/// <param name="ModelResponse">
/// The agent's aggregated response materialized as a <see cref="ChatResponse"/>. When
/// the captured run produced no output, this is an empty assistant response so evaluators
/// still receive a non-null value.
/// </param>
public readonly record struct EvaluationInputs(
    IReadOnlyList<ChatMessage> Messages,
    ChatResponse ModelResponse);
