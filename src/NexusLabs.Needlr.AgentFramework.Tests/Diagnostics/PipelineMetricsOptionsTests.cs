using NexusLabs.Needlr.AgentFramework.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="PipelineMetricsOptions"/>: defaults, mutation,
/// and the <c>ResolvedActivitySourceName</c> fallback contract.
/// </summary>
public sealed class PipelineMetricsOptionsTests
{
    [Fact]
    public void Defaults_MeterName_IsNeedlrNamespace()
    {
        var options = new PipelineMetricsOptions();
        Assert.Equal("NexusLabs.Needlr.AgentFramework.Pipelines", options.MeterName);
    }

    [Fact]
    public void Defaults_ActivitySourceName_IsNull()
    {
        var options = new PipelineMetricsOptions();
        Assert.Null(options.ActivitySourceName);
    }

    [Fact]
    public void MeterName_CanBeOverridden()
    {
        var options = new PipelineMetricsOptions { MeterName = "Custom.Pipelines" };
        Assert.Equal("Custom.Pipelines", options.MeterName);
    }

    [Fact]
    public void ActivitySourceName_CanBeOverridden()
    {
        var options = new PipelineMetricsOptions { ActivitySourceName = "Custom.PipelinesSource" };
        Assert.Equal("Custom.PipelinesSource", options.ActivitySourceName);
    }

    [Fact]
    public void ResolvedActivitySourceName_FallsBackToMeterName_WhenActivitySourceNameIsNull()
    {
        var options = new PipelineMetricsOptions { MeterName = "Custom.Pipelines" };
        Assert.Equal("Custom.Pipelines", options.ResolvedActivitySourceName);
    }

    [Fact]
    public void ResolvedActivitySourceName_UsesActivitySourceName_WhenSet()
    {
        var options = new PipelineMetricsOptions
        {
            MeterName = "Custom.Pipelines",
            ActivitySourceName = "Custom.PipelinesSource",
        };
        Assert.Equal("Custom.PipelinesSource", options.ResolvedActivitySourceName);
    }
}
