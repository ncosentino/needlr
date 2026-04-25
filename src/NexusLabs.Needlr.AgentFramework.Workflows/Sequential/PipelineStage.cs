namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Defines a single stage in a sequential pipeline, binding a name,
/// executor, and optional execution policy.
/// </summary>
/// <param name="Name">Human-readable name for the stage (used in diagnostics and progress events).</param>
/// <param name="Executor">The <see cref="IStageExecutor"/> that runs this stage's logic.</param>
/// <param name="Policy">Optional <see cref="StageExecutionPolicy"/> controlling skip, retry, and budget behavior.</param>
/// <example>
/// <code>
/// var stage = new PipelineStage(
///     "Writer",
///     new AgentStageExecutor(writerAgent, ctx => "Write a draft."),
///     new StageExecutionPolicy { MaxAttempts = 2 });
/// </code>
/// </example>
public sealed record PipelineStage(
    string Name,
    IStageExecutor Executor,
    StageExecutionPolicy? Policy = null);
