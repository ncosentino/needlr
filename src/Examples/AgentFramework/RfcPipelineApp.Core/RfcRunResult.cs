using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace RfcPipelineApp.Core;

/// <summary>
/// The result of the RFC pipeline run, bundling the pipeline outcome,
/// metadata, and final workspace state for consumers.
/// </summary>
/// <param name="PipelineResult">The raw pipeline run result with per-stage diagnostics.</param>
/// <param name="Metadata">The populated RFC metadata (title, summary, status, authors).</param>
/// <param name="Workspace">The workspace containing all generated files.</param>
public sealed record RfcRunResult(
    IPipelineRunResult PipelineResult,
    RfcMetadata Metadata,
    IWorkspace Workspace);
