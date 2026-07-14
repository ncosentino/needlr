using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

using ExperimentRunnerApp;

Console.WriteLine("=== Provider-Neutral Experiment Runner ===");
Console.WriteLine();
Console.WriteLine("This credential-free run demonstrates:");
Console.WriteLine("  - finite local cases and repeated trials");
Console.WriteLine("  - fixed bounded task concurrency");
Console.WriteLine("  - deterministic result ordering");
Console.WriteLine("  - isolated execution failure, timeout, and task cancellation");
Console.WriteLine("  - MEAI metric snapshots and a schema-versioned JSON artifact");
Console.WriteLine();

var cases = new[]
{
    new ExperimentCase<ExperimentCaseDefinition>
    {
        Id = "slow-success",
        Value = new ExperimentCaseDefinition("success", 10, 80),
        Tags = ["success", "slow"],
    },
    new ExperimentCase<ExperimentCaseDefinition>
    {
        Id = "repeated-fast-success",
        Value = new ExperimentCaseDefinition("success", 20, 10),
        TrialCount = 2,
        Tags = ["success", "repeated"],
    },
    new ExperimentCase<ExperimentCaseDefinition>
    {
        Id = "execution-failure",
        Value = new ExperimentCaseDefinition("failure", 30, 20),
        Tags = ["failure"],
    },
    new ExperimentCase<ExperimentCaseDefinition>
    {
        Id = "timeout",
        Value = new ExperimentCaseDefinition("success", 40, 250),
        Tags = ["timeout"],
    },
    new ExperimentCase<ExperimentCaseDefinition>
    {
        Id = "task-cancellation",
        Value = new ExperimentCaseDefinition("canceled", 50, 20),
        Tags = ["canceled"],
    },
};

var definition = new ExperimentDefinition<ExperimentCaseDefinition, ExperimentOutput>
{
    Name = "credential-free-phase-1",
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
            "success" => new ExperimentOutput(
                context.Case.Value.Value + context.TrialIndex,
                context.Case.Value.Value >= 20 ? "high" : "low"),
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
            new BooleanMetric("positive", context.Output.Value > 0),
            new StringMetric("category", context.Output.Category))),
};

IExperimentRunner runner = new ExperimentRunner();
var result = await runner.RunAsync(
    definition,
    new ExperimentRunOptions
    {
        RunId = "phase-1-example",
        MaxConcurrency = 2,
        AttemptTimeout = TimeSpan.FromMilliseconds(100),
    });

Console.WriteLine(
    $"Run '{result.RunId}' used {result.WorkerCount} workers for {result.Items.Count} items.");
foreach (var item in result.Items)
{
    var output = item.HasOutput
        ? item.Output!.Value.ToString()
        : "-";
    Console.WriteLine(
        $"  #{item.Sequence} {item.Case.Id} trial={item.TrialIndex} " +
        $"status={item.Status} output={output} metrics={item.Metrics.Count}");
}

foreach (var statusGroup in result.Items
    .GroupBy(item => item.Status)
    .OrderBy(group => group.Key))
{
    Console.WriteLine($"  {statusGroup.Key}: {statusGroup.Count()}");
}

var artifactPath = Path.Combine(
    Path.GetTempPath(),
    "needlr-experiment-phase-1.json");
await using (var stream = File.Create(artifactPath))
{
    await new ExperimentJsonArtifactWriter().WriteAsync(
        stream,
        result,
        ExperimentJsonContext.Default.ExperimentCaseDefinition,
        ExperimentJsonContext.Default.ExperimentOutput);
}

Console.WriteLine();
Console.WriteLine($"Schema version: {result.SchemaVersion}");
Console.WriteLine($"JSON artifact: {artifactPath}");
return 0;
