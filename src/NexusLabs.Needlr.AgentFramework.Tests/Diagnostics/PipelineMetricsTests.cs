using System.Diagnostics.Metrics;

using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="PipelineMetrics"/>: every instrument emits with the
/// expected tag schema; per-token-kind expansion fans out to one measurement
/// per non-zero kind; per-failed-tool expansion fires one measurement per
/// failed call; skip path emits stage.completed only; the 12 StageTermination
/// cases each surface as their own ``termination_cause`` tag value.
/// </summary>
public sealed class PipelineMetricsTests
{
    [Fact]
    public void Defaults_MeterAndActivitySource_UseDefaultName()
    {
        using var metrics = new PipelineMetrics();
        Assert.Equal("NexusLabs.Needlr.AgentFramework.Pipelines", metrics.ActivitySource.Name);
    }

    [Fact]
    public void CustomMeterName_AppliesToActivitySourceWhenSourceNameNull()
    {
        var options = new PipelineMetricsOptions { MeterName = "Custom.Pipelines" };
        using var metrics = new PipelineMetrics(options);
        Assert.Equal("Custom.Pipelines", metrics.ActivitySource.Name);
    }

    [Fact]
    public void CustomActivitySourceName_AppliesIndependentlyOfMeterName()
    {
        var options = new PipelineMetricsOptions
        {
            MeterName = "Custom.Pipelines",
            ActivitySourceName = "Custom.PipelinesSource",
        };
        using var metrics = new PipelineMetrics(options);
        Assert.Equal("Custom.PipelinesSource", metrics.ActivitySource.Name);
    }

    [Fact]
    public void RecordPipelineStarted_EmitsCounterTaggedWithPipelineName()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new PipelineMetrics(new PipelineMetricsOptions { MeterName = meterName });

        metrics.RecordPipelineStarted("ArticlePipeline");

        var measurement = Assert.Single(capture.LongMeasurements("pipeline.run.started"));
        Assert.Equal(1, measurement.Value);
        Assert.Equal("ArticlePipeline", measurement.Tags["pipeline_name"]);
    }

    [Fact]
    public void RecordPipelineCompleted_Succeeded_EmitsCounterAndDuration()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new PipelineMetrics(new PipelineMetricsOptions { MeterName = meterName });

        metrics.RecordPipelineCompleted("ArticlePipeline", succeeded: true, duration: TimeSpan.FromSeconds(2.5));

        var counter = Assert.Single(capture.LongMeasurements("pipeline.run.completed"));
        Assert.Equal(1, counter.Value);
        Assert.Equal("ArticlePipeline", counter.Tags["pipeline_name"]);
        Assert.Equal("Succeeded", counter.Tags["outcome"]);

        var histogram = Assert.Single(capture.DoubleMeasurements("pipeline.run.duration"));
        Assert.Equal(2.5, histogram.Value);
        Assert.Equal("Succeeded", histogram.Tags["outcome"]);
    }

    [Fact]
    public void RecordPipelineCompleted_Failed_TagsOutcomeAsFailed()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new PipelineMetrics(new PipelineMetricsOptions { MeterName = meterName });

        metrics.RecordPipelineCompleted("Pipeline", succeeded: false, duration: TimeSpan.FromSeconds(1));

        var counter = Assert.Single(capture.LongMeasurements("pipeline.run.completed"));
        Assert.Equal("Failed", counter.Tags["outcome"]);
    }

    [Fact]
    public void RecordStageCompleted_Succeeded_EmitsCompletedAndDuration()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new PipelineMetrics(new PipelineMetricsOptions { MeterName = meterName });

        var stage = StageResult(
            agentName: "Writer",
            outcome: StageOutcome.Succeeded,
            termination: new StageTermination.NaturalCompletion());

        metrics.RecordStageCompleted("Pipeline", stage, TimeSpan.FromSeconds(1));

        var completed = Assert.Single(capture.LongMeasurements("pipeline.stage.completed"));
        Assert.Equal("Pipeline", completed.Tags["pipeline_name"]);
        Assert.Equal("Writer", completed.Tags["stage_name"]);
        Assert.Equal("Succeeded", completed.Tags["outcome"]);
        Assert.Equal("NaturalCompletion", completed.Tags["termination_cause"]);
        Assert.Equal("(none)", completed.Tags["phase_name"]);

        var duration = Assert.Single(capture.DoubleMeasurements("pipeline.stage.duration"));
        Assert.Equal(1, duration.Value);
        Assert.Equal("Writer", duration.Tags["stage_name"]);
    }

    [Fact]
    public void RecordStageCompleted_Skipped_EmitsCompletedOnly()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new PipelineMetrics(new PipelineMetricsOptions { MeterName = meterName });

        var stage = StageResult(
            agentName: "Writer",
            outcome: StageOutcome.Skipped,
            termination: new StageTermination.Skipped("no work to do"));

        metrics.RecordStageCompleted("Pipeline", stage, TimeSpan.Zero);

        var completed = Assert.Single(capture.LongMeasurements("pipeline.stage.completed"));
        Assert.Equal("Skipped", completed.Tags["outcome"]);
        Assert.Equal("Skipped", completed.Tags["termination_cause"]);

        Assert.Empty(capture.DoubleMeasurements("pipeline.stage.duration"));
        Assert.Empty(capture.LongMeasurements("pipeline.stage.tokens"));
        Assert.Empty(capture.LongMeasurements("pipeline.stage.tool.failed"));
    }

    [Fact]
    public void RecordStageCompleted_PhaseName_FlowsToTagsWhenSet()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new PipelineMetrics(new PipelineMetricsOptions { MeterName = meterName });

        var stage = StageResult(agentName: "W", phaseName: "Discovery", outcome: StageOutcome.Succeeded);

        metrics.RecordStageCompleted("Pipeline", stage, TimeSpan.FromSeconds(1));

        var completed = Assert.Single(capture.LongMeasurements("pipeline.stage.completed"));
        Assert.Equal("Discovery", completed.Tags["phase_name"]);

        var duration = Assert.Single(capture.DoubleMeasurements("pipeline.stage.duration"));
        Assert.Equal("Discovery", duration.Tags["phase_name"]);
    }

    [Fact]
    public void RecordStageCompleted_NullTermination_TagsAsUnspecified()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new PipelineMetrics(new PipelineMetricsOptions { MeterName = meterName });

        var stage = StageResult(agentName: "W", outcome: StageOutcome.Succeeded, termination: null);

        metrics.RecordStageCompleted("Pipeline", stage, TimeSpan.FromSeconds(1));

        var completed = Assert.Single(capture.LongMeasurements("pipeline.stage.completed"));
        Assert.Equal("Unspecified", completed.Tags["termination_cause"]);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("NaturalCompletion")]
    [InlineData("CompletedEarlyAfterToolCall")]
    [InlineData("MaxIterationsReached")]
    [InlineData("MaxToolCallsReached")]
    [InlineData("BudgetPressure")]
    [InlineData("StallDetected")]
    [InlineData("Cancelled")]
    [InlineData("Failed")]
    [InlineData("Skipped")]
    public void RecordStageCompleted_FrameworkTerminationCases_TerminationCauseIsCaseName(string caseName)
    {
        StageTermination termination = caseName switch
        {
            "Completed" => new StageTermination.Completed(),
            "NaturalCompletion" => new StageTermination.NaturalCompletion(),
            "CompletedEarlyAfterToolCall" => new StageTermination.CompletedEarlyAfterToolCall(),
            "MaxIterationsReached" => new StageTermination.MaxIterationsReached(10, 10),
            "MaxToolCallsReached" => new StageTermination.MaxToolCallsReached(50, 50),
            "BudgetPressure" => new StageTermination.BudgetPressure(0.8),
            "StallDetected" => new StageTermination.StallDetected(3),
            "Cancelled" => new StageTermination.Cancelled(),
            "Failed" => new StageTermination.Failed(new InvalidOperationException("boom")),
            "Skipped" => new StageTermination.Skipped("no work"),
            _ => throw new InvalidOperationException(),
        };

        using var capture = new MetricCapture(out var meterName);
        using var metrics = new PipelineMetrics(new PipelineMetricsOptions { MeterName = meterName });

        var stage = StageResult(
            agentName: "W",
            outcome: termination is StageTermination.Skipped ? StageOutcome.Skipped : StageOutcome.Succeeded,
            termination: termination);

        metrics.RecordStageCompleted("Pipeline", stage, TimeSpan.FromSeconds(1));

        var completed = Assert.Single(capture.LongMeasurements("pipeline.stage.completed"));
        Assert.Equal(caseName, completed.Tags["termination_cause"]);
    }

    [Fact]
    public void RecordStageCompleted_CustomTermination_TerminationCauseIsReason()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new PipelineMetrics(new PipelineMetricsOptions { MeterName = meterName });

        var stage = StageResult(
            agentName: "W",
            outcome: StageOutcome.Succeeded,
            termination: new StageTermination.Custom("Reconciled"));

        metrics.RecordStageCompleted("Pipeline", stage, TimeSpan.FromSeconds(1));

        var completed = Assert.Single(capture.LongMeasurements("pipeline.stage.completed"));
        Assert.Equal("Reconciled", completed.Tags["termination_cause"]);
    }

    [Fact]
    public void RecordStageCompleted_TokenUsage_EmitsOneCounterPerNonZeroKind()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new PipelineMetrics(new PipelineMetricsOptions { MeterName = meterName });

        var stage = StageResult(
            agentName: "W",
            outcome: StageOutcome.Succeeded,
            diagnostics: CreateDiagnostics(tokens: new TokenUsage(
                InputTokens: 100,
                OutputTokens: 50,
                TotalTokens: 150,
                CachedInputTokens: 20,
                ReasoningTokens: 10)));

        metrics.RecordStageCompleted("Pipeline", stage, TimeSpan.FromSeconds(1));

        var tokens = capture.LongMeasurements("pipeline.stage.tokens").ToList();
        Assert.Equal(4, tokens.Count);

        Assert.Equal(100, tokens.Single(m => (string?)m.Tags["token_kind"] == "input").Value);
        Assert.Equal(50, tokens.Single(m => (string?)m.Tags["token_kind"] == "output").Value);
        Assert.Equal(20, tokens.Single(m => (string?)m.Tags["token_kind"] == "cached_input").Value);
        Assert.Equal(10, tokens.Single(m => (string?)m.Tags["token_kind"] == "reasoning").Value);
    }

    [Fact]
    public void RecordStageCompleted_TokenUsage_SkipsZeroKinds()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new PipelineMetrics(new PipelineMetricsOptions { MeterName = meterName });

        var stage = StageResult(
            agentName: "W",
            outcome: StageOutcome.Succeeded,
            diagnostics: CreateDiagnostics(tokens: new TokenUsage(
                InputTokens: 100,
                OutputTokens: 0,
                TotalTokens: 100,
                CachedInputTokens: 0,
                ReasoningTokens: 0)));

        metrics.RecordStageCompleted("Pipeline", stage, TimeSpan.FromSeconds(1));

        var tokens = capture.LongMeasurements("pipeline.stage.tokens").ToList();
        var single = Assert.Single(tokens);
        Assert.Equal("input", single.Tags["token_kind"]);
        Assert.Equal(100, single.Value);
    }

    [Fact]
    public void RecordStageCompleted_NoDiagnostics_EmitsNoTokensOrToolFailures()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new PipelineMetrics(new PipelineMetricsOptions { MeterName = meterName });

        var stage = StageResult(agentName: "W", outcome: StageOutcome.Succeeded, diagnostics: null);

        metrics.RecordStageCompleted("Pipeline", stage, TimeSpan.FromSeconds(1));

        Assert.Empty(capture.LongMeasurements("pipeline.stage.tokens"));
        Assert.Empty(capture.LongMeasurements("pipeline.stage.tool.failed"));
    }

    [Fact]
    public void RecordStageCompleted_FailedToolCalls_EmitOnePerFailureWithToolName()
    {
        using var capture = new MetricCapture(out var meterName);
        using var metrics = new PipelineMetrics(new PipelineMetricsOptions { MeterName = meterName });

        var stage = StageResult(
            agentName: "W",
            outcome: StageOutcome.Succeeded,
            diagnostics: CreateDiagnostics(toolCalls:
            [
                CreateToolCall("ReadFile", succeeded: false),
                CreateToolCall("WebSearch", succeeded: true),
                CreateToolCall("RunPython", succeeded: false),
                CreateToolCall("ReadFile", succeeded: false),
            ]));

        metrics.RecordStageCompleted("Pipeline", stage, TimeSpan.FromSeconds(1));

        var failures = capture.LongMeasurements("pipeline.stage.tool.failed").ToList();
        Assert.Equal(3, failures.Count);
        Assert.Equal(2, failures.Count(m => (string?)m.Tags["tool_name"] == "ReadFile"));
        Assert.Single(failures, m => (string?)m.Tags["tool_name"] == "RunPython");
    }

    [Fact]
    public void Dispose_DisposesMeterAndActivitySource()
    {
        var metrics = new PipelineMetrics();
        metrics.Dispose();
    }

    [Fact]
    public void RecordStageCompleted_NullStage_Throws()
    {
        using var metrics = new PipelineMetrics();
        Assert.Throws<ArgumentNullException>(() =>
            metrics.RecordStageCompleted("Pipeline", stage: null!, TimeSpan.Zero));
    }

    private static IAgentStageResult StageResult(
        string agentName,
        StageOutcome outcome = StageOutcome.Succeeded,
        StageTermination? termination = null,
        string? phaseName = null,
        IAgentRunDiagnostics? diagnostics = null) =>
        new TestStageResult(agentName, outcome, termination, phaseName, diagnostics);

    private static IAgentRunDiagnostics CreateDiagnostics(
        TokenUsage? tokens = null,
        IReadOnlyList<ToolCallDiagnostics>? toolCalls = null) =>
        new AgentRunDiagnostics(
            AgentName: "Stage",
            TotalDuration: TimeSpan.FromMilliseconds(100),
            AggregateTokenUsage: tokens ?? new TokenUsage(0, 0, 0, 0, 0),
            ChatCompletions: [],
            ToolCalls: toolCalls ?? [],
            TotalInputMessages: 1,
            TotalOutputMessages: 1,
            InputMessages: [],
            OutputResponse: null,
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow);

    private static ToolCallDiagnostics CreateToolCall(string toolName, bool succeeded) =>
        new(
            Sequence: 0,
            ToolName: toolName,
            Duration: TimeSpan.FromMilliseconds(10),
            Succeeded: succeeded,
            ErrorMessage: succeeded ? null : "boom",
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            CustomMetrics: null);

    private sealed class TestStageResult(
        string agentName,
        StageOutcome outcome,
        StageTermination? termination,
        string? phaseName,
        IAgentRunDiagnostics? diagnostics) : IAgentStageResult
    {
        public string AgentName => agentName;
        public ChatResponse? FinalResponse => null;
        public IAgentRunDiagnostics? Diagnostics => diagnostics;
        public StageOutcome Outcome => outcome;
        public string? PhaseName => phaseName;
        public StageTermination? Termination => termination;
    }

    /// <summary>
    /// Test helper that captures every <see cref="Meter"/> measurement against a
    /// per-test unique meter name. Wraps the standard
    /// <see cref="MeterListener"/> setup boilerplate so each test can assert on
    /// per-instrument measurements with their tags via
    /// <see cref="LongMeasurements"/> / <see cref="DoubleMeasurements"/>.
    /// </summary>
    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<RecordedMeasurement<long>> _longs = [];
        private readonly List<RecordedMeasurement<double>> _doubles = [];
        private readonly string _meterName;

        public MetricCapture(out string meterName)
        {
            _meterName = $"NexusLabs.Needlr.AgentFramework.Tests.{Guid.NewGuid():N}";
            meterName = _meterName;

            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == _meterName)
                    listener.EnableMeasurementEvents(instrument);
            };

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                _longs.Add(new RecordedMeasurement<long>(instrument.Name, measurement, ToDictionary(tags)));
            });

            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
            {
                _doubles.Add(new RecordedMeasurement<double>(instrument.Name, measurement, ToDictionary(tags)));
            });

            _listener.Start();
        }

        public IEnumerable<RecordedMeasurement<long>> LongMeasurements(string instrumentName) =>
            _longs.Where(m => m.InstrumentName == instrumentName);

        public IEnumerable<RecordedMeasurement<double>> DoubleMeasurements(string instrumentName) =>
            _doubles.Where(m => m.InstrumentName == instrumentName);

        public void Dispose() => _listener.Dispose();

        private static Dictionary<string, object?> ToDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var dict = new Dictionary<string, object?>(tags.Length);
            foreach (var tag in tags)
                dict[tag.Key] = tag.Value;
            return dict;
        }
    }

    private sealed record RecordedMeasurement<T>(
        string InstrumentName,
        T Value,
        Dictionary<string, object?> Tags);
}
