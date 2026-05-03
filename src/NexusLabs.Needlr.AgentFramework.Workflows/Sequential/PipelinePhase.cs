using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Defines a named group of sequential stages that share runtime configuration.
/// The <see cref="SequentialPipelineRunner"/> applies the phase's policy (lifecycle
/// hooks, token budget) before executing any stage within it.
/// </summary>
/// <remarks>
/// <para>
/// Phases represent runtime configuration boundaries, not logical groupings.
/// The same phase configuration can appear multiple times in a pipeline — each
/// <see cref="PipelinePhase"/> instance is a distinct runtime scope with its own
/// <see cref="PipelinePhasePolicy.OnEnterAsync"/> invocation and budget scope.
/// </para>
/// <para>
/// Empty phases (zero stages) are valid. <see cref="PipelinePhasePolicy.OnEnterAsync"/>
/// and progress events still fire, enabling setup-only phases.
/// </para>
/// </remarks>
/// <param name="Name">Human-readable name for the phase (used in diagnostics, progress events, and result metadata).</param>
/// <param name="Stages">The ordered list of stages within this phase.</param>
/// <param name="Policy">Optional <see cref="PipelinePhasePolicy"/> controlling lifecycle hooks and phase-level budget.</param>
/// <example>
/// <code>
/// var phases = new[]
/// {
///     new PipelinePhase("Analysis", [
///         new PipelineStage("ParseDiff", diffParser),
///         new PipelineStage("SecurityScan", securityScanner),
///     ]),
///     new PipelinePhase("Synthesis", [
///         new PipelineStage("GenerateComments", commentGenerator),
///     ], new PipelinePhasePolicy { TokenBudget = 30_000 }),
/// };
///
/// await runner.RunPhasedAsync(workspace, phases, options: null, ct);
/// </code>
/// </example>
public sealed record PipelinePhase(
    string Name,
    IReadOnlyList<PipelineStage> Stages,
    PipelinePhasePolicy? Policy = null);
