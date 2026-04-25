using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Testing;

/// <summary>
/// Defines a pipeline scenario for multi-stage agent workflow testing.
/// Extends the single-agent scenario pattern with pipeline stage construction
/// and pipeline-specific verification.
/// </summary>
/// <remarks>
/// <para>
/// Each scenario declares its own workspace seed data, pipeline stages, and
/// verification logic. The <see cref="PipelineScenarioRunner"/> handles workspace
/// creation, pipeline execution via <see cref="SequentialPipelineRunner"/>, and
/// invokes <see cref="Verify"/> after execution.
/// </para>
/// </remarks>
public interface IPipelineScenario
{
    /// <summary>Gets the scenario name (used for selection and reporting).</summary>
    string Name { get; }

    /// <summary>Gets a human-readable description of what this scenario tests.</summary>
    string Description { get; }

    /// <summary>
    /// Populates the workspace with files needed before the pipeline runs.
    /// Called by the runner before execution begins.
    /// </summary>
    /// <param name="workspace">The workspace to seed with test data.</param>
    void SeedWorkspace(IWorkspace workspace);

    /// <summary>
    /// Builds the ordered list of pipeline stages for this scenario.
    /// </summary>
    /// <param name="services">
    /// The service provider for resolving agents and other dependencies
    /// needed to construct stage executors.
    /// </param>
    /// <returns>An ordered list of stages to execute sequentially.</returns>
    IReadOnlyList<PipelineStage> BuildPipeline(IServiceProvider services);

    /// <summary>
    /// Returns optional pipeline configuration such as completion gates and
    /// total token budgets. Returns <see langword="null"/> to use defaults.
    /// </summary>
    /// <returns>Pipeline options, or <see langword="null"/>.</returns>
    SequentialPipelineOptions? GetOptions() => null;

    /// <summary>
    /// Verifies the pipeline results after execution.
    /// Throw an exception (e.g., <see cref="ScenarioVerificationException"/>) to indicate failure.
    /// </summary>
    /// <param name="workspace">The workspace after pipeline execution.</param>
    /// <param name="result">The pipeline run result containing per-stage diagnostics.</param>
    void Verify(IWorkspace workspace, IPipelineRunResult result);
}
