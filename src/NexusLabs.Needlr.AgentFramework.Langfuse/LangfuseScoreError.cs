namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes a single failed attempt to upload an evaluation score to Langfuse. Passed to
/// <see cref="LangfuseOptions.ScoreErrorCallback"/> when
/// <see cref="LangfuseScoreFailureMode.NonFatal"/> is in effect, so callers can log the loss with
/// their own logger.
/// </summary>
/// <param name="ScoreName">The name of the score that failed to upload.</param>
/// <param name="TraceId">The Langfuse trace id the score was destined for, if known.</param>
/// <param name="Exception">The exception describing the failure.</param>
public sealed record LangfuseScoreError(string ScoreName, string? TraceId, Exception Exception);
