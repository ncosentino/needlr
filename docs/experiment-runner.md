# Experiment Runner

Run a finite collection of evaluation cases with bounded concurrency, isolated failures,
deterministic ordering, and a schema-versioned JSON result—without coupling the experiment to a
specific agent loop, test framework, dataset host, or observability provider.

The Phase 1 runner lives in `NexusLabs.Needlr.AgentFramework.Evaluation.Experiments`.

## Quick Start

```csharp
using Microsoft.Extensions.AI.Evaluation;
using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

var definition = new ExperimentDefinition<MyCase, MyOutput>
{
    Name = "support-regression",
    CaseSource = new LocalExperimentCaseSource<MyCase>(
        "local-cases",
        [
            new ExperimentCase<MyCase>
            {
                Id = "refund",
                Value = new MyCase("Refund an eligible order"),
                TrialCount = 2,
                Tags = ["billing"],
            },
            new ExperimentCase<MyCase>
            {
                Id = "shipping",
                Value = new MyCase("Explain a delayed shipment"),
            },
        ]),
    Task = async (context, cancellationToken) =>
    {
        return await RunAgentAsync(
            context.Case.Value,
            cancellationToken);
    },
    ItemEvaluator = (context, cancellationToken) =>
        EvaluateAsync(
            context.Case.Value,
            context.Output,
            cancellationToken),
};

IExperimentRunner runner = new ExperimentRunner();
ExperimentRunResult<MyCase, MyOutput> result = await runner.RunAsync(
    definition,
    new ExperimentRunOptions
    {
        RunId = "commit-abc123",
        MaxConcurrency = 4,
        AttemptTimeout = TimeSpan.FromMinutes(2),
    },
    cancellationToken);
```

Every case is materialized and validated before execution. Cases expand in source order, then by
ascending one-based trial index. Results return in that same stable sequence even when completion
order differs.

## Cases, Trials, and Attempts

| Term | Meaning |
|---|---|
| Case | One logical caller-owned input with a required unique ID. |
| Trial | One statistically independent scheduled execution of a case. |
| Item | The runner's work unit for one case and one trial index. |
| Attempt | One operational execution try for an item. Phase 1 always records exactly one. |

`ExperimentCase<TCase>` keeps the caller's value and optional tags together:

```csharp
new ExperimentCase<MyCase>
{
    Id = "case-42",
    Value = myCase,
    TrialCount = 3,
    Tags = ["regression", "priority"],
}
```

`TCase` is intentionally caller-owned. Needlr does not define separate generic input,
expected-output, and metadata types.

## Validation Before Execution

The runner loads and copies the complete finite source before starting workers. It rejects:

- blank experiment names, run IDs, source names, or case IDs;
- duplicate case IDs using ordinal comparison;
- non-positive trial counts;
- non-positive `MaxConcurrency`;
- non-null attempt timeouts that are non-positive, infinite, or outside the .NET timer range;
- an expanded item count that cannot be represented safely.

Any source or validation failure throws to the caller and starts zero items.

## Bounded Scheduling

The runner expands the finite source into ordered data, then creates:

- one indexed result slot per item;
- `min(MaxConcurrency, itemCount)` long-lived worker tasks;
- one atomic next-item index.

Workers claim and execute multiple items. The runner does **not** create one task per item behind a
semaphore, so pending work stays data rather than an unbounded task collection.

`ExperimentRunResult.WorkerCount` reports the fixed worker count used by the run.

## Cancellation and Timeouts

Caller cancellation:

- stops new admission;
- reaches active tasks and item evaluation;
- wins over a simultaneous attempt timeout or execution exception;
- is rethrown from `RunAsync` with the original caller token;
- produces no completed run result or JSON artifact.

`AttemptTimeout` is cooperative. The task receives a linked caller/deadline token. Needlr continues
awaiting the task after deadline cancellation so an ignored token cannot silently release a worker
permit while the underlying operation is still active. Tasks must honor cancellation for a timely
timeout.

The terminal classifications are:

| Task/evaluation outcome | Attempt status | Item status |
|---|---|---|
| Task returns; optional evaluator succeeds | `Succeeded` | `Succeeded` |
| Task throws a non-cancellation exception | `Failed` | `ExecutionFailed` |
| Deadline requested while caller remains active | `TimedOut` | `TimedOut` |
| Task throws cancellation with neither runner token requested | `Canceled` | `Canceled` |
| Task succeeds; evaluator throws or self-cancels | `Succeeded` | `EvaluationFailed` |
| Caller token requested | No completed result | Caller cancellation rethrown |

One item failure never cancels its siblings. Failed, timed-out, canceled, and evaluation-failed
items remain in the ordered run result.

## Task and Evaluator Context

`ExperimentTaskContext<TCase>` supplies:

- run ID;
- zero-based sequence;
- materialized case;
- one-based trial index;
- one-based attempt number.

The optional `ExperimentItemEvaluator<TCase,TOutput>` runs exactly once after successful execution.
Its context includes the successful output and complete attempt history. Evaluator failure preserves
the output and never replays the task.

The task and evaluator are delegates, so DI-managed services can expose methods and assign those
method groups to the definition without introducing a second object model.

## Stable MEAI Metric Snapshots

The successful MEAI `EvaluationResult` remains available as
`ExperimentItemResult.Evaluation`, but it is mutable and is excluded from canonical JSON.

Needlr immediately freezes `ExperimentItemResult.Metrics` into provider-owned snapshots:

- numeric, boolean, string, no-value, and unknown metric kinds;
- nullable typed values;
- explicit `NaN`, positive infinity, and negative infinity classifications;
- reason and interpretation;
- ordered diagnostics;
- metadata copied in ordinal key order;
- the count of MEAI context objects omitted from schema v1.

Metrics are sorted by ordinal name. A disagreement between the `EvaluationResult.Metrics`
dictionary key and `EvaluationMetric.Name` is classified as `EvaluationFailed` rather than silently
choosing one identity.

## JSON Artifact

`ExperimentJsonArtifactWriter` writes a deterministic Needlr-owned envelope:

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(MyCase))]
[JsonSerializable(typeof(MyOutput))]
internal partial class MyJsonContext : JsonSerializerContext;

await using var stream = File.Create(path);
await new ExperimentJsonArtifactWriter().WriteAsync(
    stream,
    result,
    MyJsonContext.Default.MyCase,
    MyJsonContext.Default.MyOutput,
    cancellationToken);
```

The `JsonTypeInfo<TCase>` / `JsonTypeInfo<TOutput>` overload is the dependable trimmed and Native
AOT path. Reflection-based convenience overloads are also available and carry the standard
trimming/dynamic-code warnings.

Schema version 1 fixes:

- envelope property order;
- source/trial item ordering;
- attempt ordering;
- metric and metadata ordering;
- lower-camel enum strings;
- normalized metric/diagnostic shapes;
- structured failure fields without raw exceptions or stack traces.

Caller-owned `TCase` and `TOutput` payload schemas remain caller responsibility. The writer's
determinism applies to the Needlr envelope and normalized metrics; it does not claim RFC 8785
cryptographic canonicalization.

## Phase 1 Boundaries

This first slice deliberately does **not** provide:

- retries or retry delays;
- run evaluators or aggregate policies;
- deterministic/statistical quality decisions;
- shared concurrency limiters across runs;
- item lifecycle scopes;
- sink fan-out or publication status;
- MEAI Reporting caches/stores/reports;
- Langfuse or another provider adapter.

Those capabilities are separate phases in
[ADR-0003](adr/adr-0003-provider-neutral-experiment-runner.md).

## Runnable Example

Run the credential-free mixed-outcome example:

```bash
dotnet run --project src/Examples/AgentFramework/ExperimentRunnerApp
```

It executes successful, failed, timed-out, and task-canceled items with two workers, evaluates
successful outputs, and writes an AOT-safe schema-v1 JSON artifact under the system temporary
directory. The example project is configured for Native AOT and can be published directly.
