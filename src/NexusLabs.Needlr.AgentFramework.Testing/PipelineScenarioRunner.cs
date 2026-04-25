using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Testing;

/// <summary>
/// Runs <see cref="IPipelineScenario"/> instances using the
/// <see cref="SequentialPipelineRunner"/> and reports results.
/// </summary>
/// <remarks>
/// <para>
/// The runner handles the full seed → execute → verify lifecycle:
/// </para>
/// <list type="number">
///   <item>Creates an <see cref="InMemoryWorkspace"/> and calls <see cref="IPipelineScenario.SeedWorkspace"/>.</item>
///   <item>Builds the pipeline stages via <see cref="IPipelineScenario.BuildPipeline"/>.</item>
///   <item>Executes the pipeline using <see cref="SequentialPipelineRunner"/>.</item>
///   <item>Calls <see cref="IPipelineScenario.Verify"/> with the post-execution workspace and result.</item>
/// </list>
/// </remarks>
[DoNotAutoRegister]
public sealed class PipelineScenarioRunner
{
    private readonly SequentialPipelineRunner _pipelineRunner;

    /// <summary>
    /// Initializes a new <see cref="PipelineScenarioRunner"/>.
    /// </summary>
    /// <param name="pipelineRunner">The sequential pipeline runner used to execute stages.</param>
    public PipelineScenarioRunner(SequentialPipelineRunner pipelineRunner)
    {
        _pipelineRunner = pipelineRunner ?? throw new ArgumentNullException(nameof(pipelineRunner));
    }

    /// <summary>
    /// Runs the scenario with full lifecycle management: seed workspace,
    /// build pipeline, execute, and verify outcomes.
    /// </summary>
    /// <param name="scenario">The pipeline scenario to run.</param>
    /// <param name="services">Service provider for resolving pipeline dependencies.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the scenario run.</returns>
    public async Task<PipelineScenarioResult> RunAsync(
        IPipelineScenario scenario,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(services);

        var workspace = new InMemoryWorkspace();
        scenario.SeedWorkspace(workspace);

        var stages = scenario.BuildPipeline(services);
        var options = scenario.GetOptions();

        var result = await _pipelineRunner.RunAsync(
            workspace,
            stages,
            options,
            cancellationToken);

        scenario.Verify(workspace, result);

        return new PipelineScenarioResult(scenario.Name, workspace, result);
    }
}
