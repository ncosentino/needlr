using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation;
using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

using ExperimentRunnerApp;

Console.WriteLine("=== Provider-Neutral Experiment Runner ===");
Console.WriteLine();
Console.WriteLine("This credential-free run demonstrates:");
Console.WriteLine("  - seeded stochastic trials");
Console.WriteLine("  - explicit retries that re-enter the scheduler after a delay");
Console.WriteLine("  - local and caller-owned shared concurrency limits");
Console.WriteLine("  - isolated execution failure, timeout, and task cancellation");
Console.WriteLine("  - run evaluation plus deterministic and statistical policies");
Console.WriteLine("  - schema-v3 deterministic JSON output");
Console.WriteLine();

var cases = new[]
{
    new ExperimentCase<ExperimentCaseDefinition>
    {
        Id = "seeded-stochastic",
        Value = new ExperimentCaseDefinition(
            "stochastic",
            137,
            5,
            0.85),
        TrialCount = 40,
        Tags = ["stochastic", "repeated"],
    },
    new ExperimentCase<ExperimentCaseDefinition>
    {
        Id = "retry-once",
        Value = new ExperimentCaseDefinition(
            "retry",
            20,
            5,
            1),
        Tags = ["retry"],
    },
    new ExperimentCase<ExperimentCaseDefinition>
    {
        Id = "execution-failure",
        Value = new ExperimentCaseDefinition(
            "failure",
            30,
            5,
            0),
        Tags = ["failure"],
    },
    new ExperimentCase<ExperimentCaseDefinition>
    {
        Id = "timeout",
        Value = new ExperimentCaseDefinition(
            "success",
            40,
            250,
            1),
        Tags = ["timeout"],
    },
    new ExperimentCase<ExperimentCaseDefinition>
    {
        Id = "task-cancellation",
        Value = new ExperimentCaseDefinition(
            "canceled",
            50,
            5,
            0),
        Tags = ["canceled"],
    },
};

var definition = new ExperimentDefinition<ExperimentCaseDefinition, ExperimentOutput>
{
    Name = "credential-free-phase-2",
    CaseSource = new LocalExperimentCaseSource<ExperimentCaseDefinition>(
        "local-example",
        cases),
    Task = async (context, cancellationToken) =>
    {
        await Task.Delay(
            context.Case.Value.DelayMilliseconds,
            cancellationToken);
        return context.Case.Value.Mode switch
        {
            "stochastic" => new ExperimentOutput(
                context.Case.Value.Value + context.TrialIndex,
                "stochastic",
                Sample(
                    context.Case.Value.Value,
                    context.TrialIndex,
                    context.Case.Value.SuccessProbability)),
            "retry" when context.AttemptNumber == 1 =>
                throw new InvalidOperationException("Scripted transient failure."),
            "retry" => new ExperimentOutput(
                context.Case.Value.Value,
                "retried",
                Passed: true),
            "success" => new ExperimentOutput(
                context.Case.Value.Value,
                "success",
                Passed: true),
            "failure" => throw new InvalidOperationException(
                "Scripted execution failure."),
            "canceled" => throw new OperationCanceledException(
                "Scripted task-originated cancellation."),
            _ => throw new InvalidOperationException(
                $"Unknown mode '{context.Case.Value.Mode}'."),
        };
    },
    ItemEvaluator = (context, _) =>
        ValueTask.FromResult(new EvaluationResult(
            new NumericMetric("value", context.Output.Value),
            new BooleanMetric("passed", context.Output.Passed),
            new StringMetric("category", context.Output.Category))),
    RunEvaluators =
    [
        new ExperimentRunEvaluator<ExperimentCaseDefinition, ExperimentOutput>(
            "aggregate",
            (context, _) =>
            {
                var successfulItems = context.Items.Count(
                    item => item.Status == ExperimentItemStatus.Succeeded);
                var completionRate = context.Items.Count == 0
                    ? 0
                    : (double)successfulItems / context.Items.Count;
                return ValueTask.FromResult(new EvaluationResult(
                    new NumericMetric("completion_rate", completionRate),
                    new NumericMetric(
                        "attempt_count",
                        context.Items.Sum(item => item.Attempts.Count))));
            }),
    ],
    Policies =
    [
        new ExperimentRunEvaluationThresholdPolicy<
            ExperimentCaseDefinition,
            ExperimentOutput>(
            "completion",
            "aggregate",
            new EvaluationThresholdEvaluator()
                .RequireNumericMin("completion_rate", 0.8)),
        new ExperimentBinarySuccessPolicy<
            ExperimentCaseDefinition,
            ExperimentOutput>(
            "binary-success",
            "passed",
            requiredSuccessRate: 0.5,
            minimumSampleCount: 20,
            confidenceLevel: 0.95),
    ],
};

await using var sharedLimiter = new ExperimentConcurrencyLimiter(
    maximumConcurrency: 3);
IExperimentRunner runner = new ExperimentRunner();
var result = await runner.RunAsync(
    definition,
    new ExperimentRunOptions
    {
        RunId = "phase-2-example",
        MaxConcurrency = 4,
        AttemptTimeout = TimeSpan.FromMilliseconds(100),
        RetryPolicy = new ExperimentRetryPolicy(
            maxAttempts: 2,
            retryOn: ExperimentRetryableOutcome.ExecutionFailure,
            delay: TimeSpan.FromMilliseconds(20)),
        SharedLimiter = sharedLimiter,
    });

Console.WriteLine(
    $"Run '{result.RunId}' used {result.WorkerCount} workers for {result.Items.Count} trials.");
foreach (var statusGroup in result.Items
    .GroupBy(item => item.Status)
    .OrderBy(group => group.Key))
{
    Console.WriteLine($"  {statusGroup.Key}: {statusGroup.Count()}");
}

Console.WriteLine(
    $"  Operational attempts: {result.Items.Sum(item => item.Attempts.Count)}");
foreach (var policy in result.PolicyResults)
{
    Console.WriteLine(
        $"  Policy '{policy.Name}': {policy.Decision} " +
        $"({policy.Kind}, required={policy.IsRequired})");
    if (policy.StatisticalEvidence is { } statistics)
    {
        Console.WriteLine(
            $"    samples={statistics.SampleCount}, " +
            $"successes={statistics.SuccessCount}, " +
            $"failures={statistics.FailureCount}, " +
            $"exclusions={statistics.ExclusionCount}");
        Console.WriteLine(
            $"    estimate={statistics.Estimate:P1}, " +
            $"one-sided {statistics.ConfidenceLevel:P0} bounds=" +
            $"[{statistics.OneSidedLowerBound:P1}, {statistics.OneSidedUpperBound:P1}]");
    }
}

var artifactPath = Path.Combine(
    Path.GetTempPath(),
    "needlr-experiment-phase-2.json");
await using (var stream = File.Create(artifactPath))
{
    await new ExperimentJsonArtifactWriter().WriteAsync(
        stream,
        result,
        ExperimentJsonContext.Default.ExperimentCaseDefinition,
        ExperimentJsonContext.Default.ExperimentOutput);
}

Console.WriteLine();
Console.WriteLine($"Overall decision: {result.Decision}");
Console.WriteLine($"Schema version: {result.SchemaVersion}");
Console.WriteLine($"JSON artifact: {artifactPath}");
return 0;

static bool Sample(
    int seed,
    int trialIndex,
    double successProbability)
{
    var trialSeed = unchecked(seed * 397 ^ trialIndex * 7919);
    return new Random(trialSeed).NextDouble() < successProbability;
}
