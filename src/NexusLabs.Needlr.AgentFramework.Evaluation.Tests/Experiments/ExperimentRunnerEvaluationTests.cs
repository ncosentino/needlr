using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentRunnerEvaluationTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_ItemEvaluator_FreezesNormalizedMetrics()
    {
        var numeric = new NumericMetric("z_numeric", 0.75, "numeric reason")
        {
            Interpretation = new EvaluationMetricInterpretation(
                EvaluationRating.Good,
                failed: false,
                reason: "good enough"),
        };
        numeric.AddDiagnostics(
            EvaluationDiagnostic.Warning("check calibration"));
        numeric.AddOrUpdateMetadata("zeta", "last");
        numeric.AddOrUpdateMetadata("alpha", "first");
        var boolean = new BooleanMetric("a_boolean", true, "boolean reason");
        var text = new StringMetric("m_string", "accepted", "string reason");
        var evaluation = new EvaluationResult(numeric, boolean, text);
        var evaluatorCalls = 0;
        var definition = CreateDefinition(
            (_, _) => ValueTask.FromResult(42),
            (_, _) =>
            {
                Interlocked.Increment(ref evaluatorCalls);
                return ValueTask.FromResult(evaluation);
            });
        var runner = new ExperimentRunner();

        var result = await runner.RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(1, evaluatorCalls);
        Assert.Same(evaluation, item.Evaluation);
        Assert.Equal(
            ["a_boolean", "m_string", "z_numeric"],
            item.Metrics.Select(metric => metric.Name).ToArray());

        var numericSnapshot = item.Metrics[2];
        Assert.Equal(ExperimentMetricKind.Numeric, numericSnapshot.Kind);
        Assert.Equal(0.75, numericSnapshot.NumericValue);
        Assert.Equal("numeric reason", numericSnapshot.Reason);
        Assert.Equal(ExperimentMetricRating.Good, numericSnapshot.Interpretation!.Rating);
        Assert.False(
            numericSnapshot.Interpretation.Failed,
            "Expected the metric interpretation failure flag to be preserved.");
        Assert.Equal("good enough", numericSnapshot.Interpretation.Reason);
        var diagnostic = Assert.Single(numericSnapshot.Diagnostics);
        Assert.Equal(ExperimentMetricDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("check calibration", diagnostic.Message);
        Assert.Equal(
            ["alpha", "zeta"],
            numericSnapshot.Metadata.Keys.ToArray());

        numeric.Value = 0.1;
        numeric.Reason = "mutated";
        numeric.Diagnostics!.Clear();
        numeric.Metadata!.Clear();

        Assert.Equal(0.75, numericSnapshot.NumericValue);
        Assert.Equal("numeric reason", numericSnapshot.Reason);
        Assert.Single(numericSnapshot.Diagnostics);
        Assert.Equal(2, numericSnapshot.Metadata.Count);
    }

    [Fact]
    public async Task RunAsync_EvaluatorFailure_PreservesOutputAndDoesNotReplayExecution()
    {
        var executions = 0;
        var evaluatorCalls = 0;
        var definition = CreateDefinition(
            (_, _) =>
            {
                Interlocked.Increment(ref executions);
                return ValueTask.FromResult(42);
            },
            (_, _) =>
            {
                Interlocked.Increment(ref evaluatorCalls);
                throw new InvalidOperationException("evaluation failed");
            });
        var runner = new ExperimentRunner();

        var result = await runner.RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(1, executions);
        Assert.Equal(1, evaluatorCalls);
        Assert.Equal(ExperimentItemStatus.EvaluationFailed, item.Status);
        Assert.True(item.HasOutput, "Expected successful task output to survive evaluator failure.");
        Assert.Equal(42, item.Output);
        Assert.Equal(
            ExperimentAttemptStatus.Succeeded,
            Assert.Single(item.Attempts).Status);
        Assert.Equal(ExperimentFailureCode.EvaluationFailed, item.Failure!.Code);
        Assert.Equal(ExperimentFailureStage.ItemEvaluation, item.Failure.Stage);
        Assert.Empty(item.Metrics);
    }

    [Fact]
    public async Task RunAsync_CallerCancellationDuringEvaluation_PropagatesExactToken()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        var evaluationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = CreateDefinition(
            (_, _) => ValueTask.FromResult(42),
            async (_, token) =>
            {
                evaluationStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new EvaluationResult();
            });
        var runner = new ExperimentRunner();
        var runTask = runner.RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            cancellation.Token);
        await evaluationStarted.Task.WaitAsync(_cancellationToken);

        cancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task RunAsync_MetricDictionaryNameMismatch_ClassifiesEvaluationFailed()
    {
        var metric = new NumericMetric("actual-name", 1);
        var evaluation = new EvaluationResult();
        evaluation.Metrics["different-key"] = metric;
        var runner = new ExperimentRunner();

        var result = await runner.RunAsync(
            CreateDefinition(
                (_, _) => ValueTask.FromResult(42),
                (_, _) => ValueTask.FromResult(evaluation)),
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(ExperimentItemStatus.EvaluationFailed, item.Status);
        Assert.True(item.HasOutput, "Expected task output to survive normalization failure.");
        Assert.Contains("different-key", item.Failure!.Message, StringComparison.Ordinal);
        Assert.Contains("actual-name", item.Failure.Message, StringComparison.Ordinal);
    }

    private static ExperimentDefinition<int, int> CreateDefinition(
        ExperimentTask<int, int> task,
        ExperimentItemEvaluator<int, int> evaluator) =>
        new()
        {
            Name = "evaluation",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                [new ExperimentCase<int> { Id = "case-1", Value = 1 }]),
            Task = task,
            ItemEvaluator = evaluator,
        };
}
