using System.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// No-op <see cref="IPipelineMetrics"/> implementation. Registered as the DI
/// default when the consumer has not configured <see cref="PipelineMetricsOptions"/>
/// via <c>ConfigurePipelineMetrics</c> on the agent-framework syringe — pipeline
/// observability is opt-in with zero overhead by default. Same posture as
/// <see cref="IAgentMetrics"/> when no <see cref="AgentFrameworkMetricsOptions"/>
/// is configured at the <c>IAgentMetrics</c> level today.
/// </summary>
/// <remarks>
/// All <see cref="IPipelineMetrics"/> methods are no-ops. The exposed
/// <see cref="ActivitySource"/> uses a unique source name with the suffix
/// <c>".NoOp"</c> so that an unintentional listener attached to
/// <c>"NexusLabs.Needlr.AgentFramework.Pipelines"</c> does not accidentally pick
/// up no-op activities — listeners must explicitly opt into the <c>".NoOp"</c>
/// source if they want to capture this no-op output (which they almost never do).
/// </remarks>
[DoNotAutoRegister]
internal sealed class NoOpPipelineMetrics : IPipelineMetrics, IDisposable
{
    private readonly ActivitySource _activitySource = new("NexusLabs.Needlr.AgentFramework.Pipelines.NoOp");

    public ActivitySource ActivitySource => _activitySource;

    public void RecordPipelineStarted(string pipelineName) { }

    public void RecordPipelineCompleted(string pipelineName, bool succeeded, TimeSpan duration) { }

    public void RecordStageCompleted(string pipelineName, IAgentStageResult stage, TimeSpan duration) { }

    public void Dispose() => _activitySource.Dispose();
}
