using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// LLM-judged evaluator that assesses whether an agent actually accomplished
/// the task it was given. Unlike MEAI's <c>TaskAdherenceEvaluator</c> (which
/// checks instruction following), this evaluator checks <em>task success</em>:
/// did the agent produce output that satisfies the original request?
/// </summary>
/// <remarks>
/// <para>
/// This evaluator requires a <see cref="ChatConfiguration"/> with a judge
/// <see cref="IChatClient"/>. It sends the original prompt and agent output
/// to the judge with a structured evaluation prompt and parses the response.
/// </para>
/// <para>
/// When no judge is configured (<c>chatConfiguration</c> is null
/// or has no <see cref="ChatConfiguration.ChatClient"/>), the evaluator
/// returns an empty <see cref="EvaluationResult"/>.
/// </para>
/// <para>
/// Metrics produced:
/// </para>
/// <list type="bullet">
///   <item><description><c>Task Completed</c> — boolean. <see langword="true"/> when the
///   judge determines the agent accomplished the requested task.</description></item>
///   <item><description><c>Task Completion Score</c> — numeric (1–5). How completely and
///   correctly the agent fulfilled the request. 5 = fully complete, 1 = not started or
///   completely wrong.</description></item>
///   <item><description><c>Task Completion Reasoning</c> — string. The judge's
///   explanation for the score.</description></item>
/// </list>
/// </remarks>
public sealed partial class TaskCompletionEvaluator : IEvaluator
{
    /// <summary>Metric name for the boolean task-completed flag.</summary>
    public const string TaskCompletedMetricName = "Task Completed";

    /// <summary>Metric name for the numeric 1–5 completion score.</summary>
    public const string TaskCompletionScoreMetricName = "Task Completion Score";

    /// <summary>Metric name for the judge's reasoning.</summary>
    public const string TaskCompletionReasoningMetricName = "Task Completion Reasoning";

    /// <summary>Score threshold at or above which the task is considered completed.</summary>
    public const int CompletionThreshold = 3;

    private const string SystemPrompt = """
        You are an evaluation judge. Your job is to assess whether an AI agent
        completed a task it was given.

        You will be given:
        1. The original task/request (USER PROMPT)
        2. The agent's final output (AGENT OUTPUT)

        Rate the agent's task completion on a scale of 1-5:
        - 5: Task fully completed with correct, comprehensive output
        - 4: Task mostly completed with minor gaps or issues
        - 3: Task partially completed — core intent addressed but significant gaps
        - 2: Task barely started — some relevant content but far from complete
        - 1: Task not completed — output is wrong, empty, or irrelevant

        Respond with EXACTLY this format (no markdown, no extra text):
        SCORE: <number 1-5>
        COMPLETED: <YES or NO>
        REASONING: <one paragraph explaining your assessment>
        """;

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        TaskCompletedMetricName,
        TaskCompletionScoreMetricName,
        TaskCompletionReasoningMetricName,
    ];

    /// <inheritdoc />
    public async ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        if (chatConfiguration?.ChatClient is null)
        {
            return new EvaluationResult();
        }

        var userPrompt = ExtractUserPrompt(messages);
        var agentOutput = modelResponse.Text ?? string.Empty;

        var diagnosticsContext = additionalContext?
            .OfType<AgentRunDiagnosticsContext>()
            .FirstOrDefault()?
            .Diagnostics;

        var evaluationPrompt = BuildEvaluationPrompt(userPrompt, agentOutput, diagnosticsContext);

        var judgeMessages = new ChatMessage[]
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, evaluationPrompt),
        };

        var judgeResponse = await chatConfiguration.ChatClient
            .GetResponseAsync(judgeMessages, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var judgeText = judgeResponse.Text ?? string.Empty;
        var (score, completed, reasoning) = ParseJudgeResponse(judgeText);

        return new EvaluationResult(
            new BooleanMetric(
                TaskCompletedMetricName,
                value: completed,
                reason: completed
                    ? "The judge determined the agent accomplished the task."
                    : "The judge determined the agent did not accomplish the task."),
            new NumericMetric(
                TaskCompletionScoreMetricName,
                value: score,
                reason: $"Score {score}/5 (threshold for completion: {CompletionThreshold})."),
            new StringMetric(
                TaskCompletionReasoningMetricName,
                value: reasoning,
                reason: "The judge's explanation for the task completion assessment."));
    }

    private static string ExtractUserPrompt(IEnumerable<ChatMessage> messages)
    {
        foreach (var msg in messages)
        {
            if (msg.Role == ChatRole.User && !string.IsNullOrWhiteSpace(msg.Text))
            {
                return msg.Text;
            }
        }

        return "(no user prompt provided)";
    }

    private static string BuildEvaluationPrompt(
        string userPrompt,
        string agentOutput,
        IAgentRunDiagnostics? diagnostics)
    {
        var prompt = $"""
            USER PROMPT:
            {userPrompt}

            AGENT OUTPUT:
            {(string.IsNullOrWhiteSpace(agentOutput) ? "(empty — the agent produced no text output)" : agentOutput)}
            """;

        if (diagnostics is not null)
        {
            prompt += $"""


                ADDITIONAL CONTEXT:
                - Tool calls made: {diagnostics.ToolCalls.Count}
                - Tool calls failed: {diagnostics.ToolCalls.Count(t => !t.Succeeded)}
                - Execution mode: {diagnostics.ExecutionMode ?? "unknown"}
                - Agent reported success: {diagnostics.Succeeded}
                """;
        }

        return prompt;
    }

    internal static (int Score, bool Completed, string Reasoning) ParseJudgeResponse(string response)
    {
        var score = 1;
        var completed = false;
        var reasoning = "Unable to parse judge response.";

        var scoreMatch = ScorePattern().Match(response);
        if (scoreMatch.Success &&
            int.TryParse(scoreMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var parsedScore))
        {
            score = Math.Clamp(parsedScore, 1, 5);
        }

        var completedMatch = CompletedPattern().Match(response);
        if (completedMatch.Success)
        {
            completed = string.Equals(
                completedMatch.Groups[1].Value.Trim(),
                "YES",
                StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            completed = score >= CompletionThreshold;
        }

        var reasoningMatch = ReasoningPattern().Match(response);
        if (reasoningMatch.Success)
        {
            reasoning = reasoningMatch.Groups[1].Value.Trim();
        }

        return (score, completed, reasoning);
    }

    [GeneratedRegex(@"SCORE:\s*(\d)", RegexOptions.IgnoreCase)]
    private static partial Regex ScorePattern();

    [GeneratedRegex(@"COMPLETED:\s*(YES|NO)", RegexOptions.IgnoreCase)]
    private static partial Regex CompletedPattern();

    [GeneratedRegex(@"REASONING:\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ReasoningPattern();
}
