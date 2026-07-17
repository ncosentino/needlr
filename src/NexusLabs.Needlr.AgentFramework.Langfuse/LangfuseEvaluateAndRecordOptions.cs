using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Configures evaluator execution and Langfuse score projection for one
/// <see cref="LangfuseEvaluationScoreExtensions"/> evaluate-and-record operation.
/// </summary>
public sealed record LangfuseEvaluateAndRecordOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LangfuseEvaluateAndRecordOptions"/> class.
    /// </summary>
    /// <param name="chatConfiguration">
    /// The chat configuration for evaluators that require an AI judge, or <see langword="null"/>.
    /// </param>
    /// <param name="additionalContext">
    /// Additional evaluation contexts, or <see langword="null"/> to use an empty collection. The
    /// contexts are snapshotted during construction.
    /// </param>
    /// <param name="scoreOptions">
    /// Stable identity settings for projected Langfuse metric scores, or <see langword="null"/>.
    /// </param>
    public LangfuseEvaluateAndRecordOptions(
        ChatConfiguration? chatConfiguration,
        IEnumerable<EvaluationContext>? additionalContext,
        LangfuseEvaluationScoreOptions? scoreOptions)
    {
        ChatConfiguration = chatConfiguration;
        AdditionalContext = Array.AsReadOnly(additionalContext?.ToArray() ?? []);
        ScoreOptions = scoreOptions is null ? null : scoreOptions with { };
    }

    /// <summary>
    /// Gets the chat configuration for evaluators that require an AI judge.
    /// </summary>
    public ChatConfiguration? ChatConfiguration { get; }

    /// <summary>
    /// Gets the snapshotted additional evaluation contexts.
    /// </summary>
    public IReadOnlyCollection<EvaluationContext> AdditionalContext { get; }

    /// <summary>
    /// Gets the stable identity settings for projected Langfuse metric scores.
    /// </summary>
    public LangfuseEvaluationScoreOptions? ScoreOptions { get; }
}
