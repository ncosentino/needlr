using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Testing;

/// <summary>
/// Result of running a single <see cref="IPipelineScenario"/>.
/// </summary>
/// <param name="ScenarioName">The name of the scenario that was executed.</param>
/// <param name="Workspace">The workspace after pipeline execution.</param>
/// <param name="PipelineResult">The pipeline run result containing per-stage diagnostics.</param>
public sealed record PipelineScenarioResult(
    string ScenarioName,
    IWorkspace Workspace,
    IPipelineRunResult PipelineResult);
