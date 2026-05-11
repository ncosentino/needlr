using NexusLabs.Needlr.AgentFramework.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="NoOpPipelineMetrics"/>: confirms every method is a no-op
/// and that the exposed <see cref="System.Diagnostics.ActivitySource"/> uses a
/// dedicated <c>".NoOp"</c>-suffixed source name so it does not accidentally pick
/// up listeners targeting the real pipeline meter source.
/// </summary>
public sealed class NoOpPipelineMetricsTests
{
    [Fact]
    public void RecordPipelineStarted_DoesNotThrow()
    {
        using var metrics = new NoOpPipelineMetrics();
        metrics.RecordPipelineStarted("Pipeline");
    }

    [Fact]
    public void RecordPipelineCompleted_DoesNotThrow()
    {
        using var metrics = new NoOpPipelineMetrics();
        metrics.RecordPipelineCompleted("Pipeline", succeeded: true, duration: TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RecordStageCompleted_DoesNotThrow()
    {
        using var metrics = new NoOpPipelineMetrics();
        var stage = new MinimalStageResult();
        metrics.RecordStageCompleted("Pipeline", stage, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void ActivitySource_UsesNoOpSuffixedName_SoListenersOnRealSourceDoNotPickItUp()
    {
        using var metrics = new NoOpPipelineMetrics();
        Assert.Equal("NexusLabs.Needlr.AgentFramework.Pipelines.NoOp", metrics.ActivitySource.Name);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var metrics = new NoOpPipelineMetrics();
        metrics.Dispose();
    }

    private sealed class MinimalStageResult : IAgentStageResult
    {
        public string AgentName => "Stage";
        public Microsoft.Extensions.AI.ChatResponse? FinalResponse => null;
        public IAgentRunDiagnostics? Diagnostics => null;
    }
}
