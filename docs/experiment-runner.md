# Experiment Runner

Run finite evaluation cases and repeated trials with bounded concurrency, delayed retries,
run-level measurement, structured quality policies, and deterministic JSON—without coupling the
experiment to a specific agent loop, test framework, dataset host, or observability provider.

The runner lives in `NexusLabs.Needlr.AgentFramework.Evaluation.Experiments`.

## Quick Start

```csharp
using Microsoft.Extensions.AI.Evaluation;
using NexusLabs.Needlr.AgentFramework.Evaluation;
using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

var thresholds = new EvaluationThresholdEvaluator()
    .RequireNumericMin("completion_rate", 0.95);

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
                TrialCount = 30,
                Tags = ["billing"],
            },
        ]),
    Task = async (context, cancellationToken) =>
        await RunAgentAsync(context.Case.Value, cancellationToken),
    ItemEvaluator = (context, cancellationToken) =>
        EvaluateAsync(
            context.Case.Value,
            context.Output,
            cancellationToken),
    RunEvaluators =
    [
        new ExperimentRunEvaluator<MyCase, MyOutput>(
            "aggregate",
            (context, _) =>
            {
                var completionRate =
                    (double)context.Items.Count(x => x.Status == ExperimentItemStatus.Succeeded)
                    / context.Items.Count;
                return ValueTask.FromResult(new EvaluationResult(
                    new NumericMetric("completion_rate", completionRate)));
            }),
    ],
    Policies =
    [
        new ExperimentRunEvaluationThresholdPolicy<MyCase, MyOutput>(
            "completion",
            "aggregate",
            thresholds),
        new ExperimentBinarySuccessPolicy<MyCase, MyOutput>(
            "binary-success",
            "passed",
            requiredSuccessRate: 0.9,
            minimumSampleCount: 30,
            confidenceLevel: 0.95),
    ],
};

await using var sharedLimiter = new ExperimentConcurrencyLimiter(8);
ExperimentRunResult<MyCase, MyOutput> result =
    await new ExperimentRunner().RunAsync(
        definition,
        new ExperimentRunOptions
        {
            RunId = "commit-abc123",
            MaxConcurrency = 4,
            AttemptTimeout = TimeSpan.FromMinutes(2),
            RetryPolicy = new ExperimentRetryPolicy(
                maxAttempts: 3,
                retryOn: ExperimentRetryableOutcome.ExecutionFailure
                    | ExperimentRetryableOutcome.Timeout,
                delay: TimeSpan.FromSeconds(1)),
            SharedLimiter = sharedLimiter,
        },
        cancellationToken);
```

Every case is materialized and validated before execution. Cases expand in source order, then by
ascending one-based trial index. Items return in that stable order regardless of completion or retry
order.

## Cases, Trials, and Attempts

| Term | Meaning |
|---|---|
| Case | One logical caller-owned input with a required unique ID. |
| Trial | One statistically independent scheduled execution of a case. |
| Item | The runner's work unit for one case and one trial index. |
| Attempt | One operational execution try for an item. Retries add attempts, never trials. |

`ExperimentCase<TCase>.TrialCount` controls statistical replication. `TCase` remains caller-owned,
so expected outputs, metadata, and provider references can live in one domain-specific value.

`ExperimentTaskContext<TCase>` supplies the run ID, stable sequence, materialized case, one-based
trial index, and one-based attempt number.

## Validation Before Execution

The runner rejects invalid static configuration before starting any item:

- blank experiment, run, source, case, run-evaluator, or policy names;
- duplicate case IDs, run-evaluator names, or policy names using ordinal comparison;
- non-positive trial counts or `MaxConcurrency`;
- non-positive retry-policy attempt limits;
- invalid attempt timeouts;
- an expanded item count that cannot be represented safely.

Source-loading and validation failures throw to the caller. Per-item execution, item-evaluation,
run-evaluation, and policy failures are represented structurally after execution begins.

## Ready and Delayed Scheduling

The runner uses:

- one bounded ready channel containing item state;
- `min(MaxConcurrency, itemCount)` long-lived worker tasks;
- one timed priority queue for delayed retries;
- one dispatcher that promotes due retries back into the ready channel;
- one indexed result slot per statistical trial.

Pending work remains data rather than one task per item. A delayed retry is not attached to a
sleeping worker: its item state sits in the timed queue until its ready time. This lets workers run
other ready items while retries are parked.

`ExperimentRunResult.WorkerCount` reports the local worker count. `MaxConcurrency` bounds active
execution attempts, not delayed retries, item evaluation, or total queued work.

## Explicit Retries

No retry policy means exactly one attempt, preserving the default behavior.

`ExperimentRetryPolicy` requires:

- a maximum total attempt count, including the initial attempt;
- explicit eligible outcomes;
- either a fixed delay or a caller-supplied delay function.

Eligible outcomes are execution failure, timeout, and task-originated cancellation. Caller
cancellation is never offered to the retry policy. Item evaluation, run evaluation, and quality
policy failures never replay task execution.

Each `ExperimentAttemptResult` records:

- attempt number, status, start time, and duration;
- the structured execution failure, when present;
- `DelayBeforeNextAttempt` when another attempt was scheduled.

`DelayBeforeNextAttempt = TimeSpan.Zero` means an immediate re-enqueue. `null` means no attempt
followed. A failure selected for retry has `IsRetryable = true`; an exhausted or unselected terminal
failure does not claim another retry.

Custom `IExperimentRetryPolicy` implementations remain bounded by `MaxAttempts`. Invalid decisions
or policy exceptions become an item-level `RetryPolicyFailed` failure rather than disappearing or
failing unrelated items. Hidden random jitter is not added. A jittered delay function must use
caller-controlled deterministic state when reproducibility matters.

## Shared Concurrency Limiter

`MaxConcurrency` is local to one run. Set `ExperimentRunOptions.SharedLimiter` when simultaneous
runs must share a second admission boundary:

```csharp
await using var limiter = new ExperimentConcurrencyLimiter(12);

var first = runner.RunAsync(
    firstDefinition,
    new ExperimentRunOptions
    {
        RunId = "first",
        MaxConcurrency = 8,
        SharedLimiter = limiter,
    },
    cancellationToken);
var second = runner.RunAsync(
    secondDefinition,
    new ExperimentRunOptions
    {
        RunId = "second",
        MaxConcurrency = 8,
        SharedLimiter = limiter,
    },
    cancellationToken);
```

The runner:

- acquires one `IExperimentConcurrencyLimiter` lease per attempt;
- awaits acquisition with the caller token;
- disposes the lease when the attempt actually completes;
- releases the lease before computing or waiting through a retry delay;
- never disposes the caller-owned limiter.

The built-in `ExperimentConcurrencyLimiter` is semaphore-backed and suitable for DI registration.
Alternate implementations can adapt provider quotas or resource-aware admission logic.

## Cancellation and Timeouts

Caller cancellation:

- stops ready-channel admission, limiter waits, active tasks, retry delays, item evaluation, run
  evaluation, and policies;
- wins over simultaneous timeout or execution failure;
- is rethrown from `RunAsync` with the original caller token;
- produces no completed run result.

`AttemptTimeout` is cooperative and restarts for every attempt. The task receives a linked
caller/deadline token. If a task ignores deadline cancellation, the runner continues awaiting it so
worker and shared-limiter accounting remain truthful. An output returned after deadline expiration
is discarded and classified `TimedOut`.

## Item Evaluation

The optional `ExperimentItemEvaluator<TCase,TOutput>` runs once after terminal execution success.
It receives the successful output and complete attempt history.

Evaluator failure:

- preserves the successful task output;
- marks the item `EvaluationFailed`;
- does not invoke the retry policy;
- never replays execution.

The mutable MEAI `EvaluationResult` remains available on the item but is excluded from canonical
JSON. Needlr immediately freezes its metrics, diagnostics, interpretations, and metadata into
ordered `ExperimentMetricSnapshot` values.

## Run Evaluators

Run evaluators measure; policies decide.

Each named `IExperimentRunEvaluator<TCase,TOutput>` receives every item in stable sequence order,
including execution failures, timeouts, task-originated cancellations, and evaluation failures.
Evaluators run sequentially in registration order.

A successful evaluator retains its MEAI `EvaluationResult` and frozen metrics. An exception or
normalization failure produces `ExperimentRunEvaluationStatus.Failed` with a structured
`RunEvaluationFailed` failure, then later evaluators still run.

## Deterministic Policies

`ExperimentRunEvaluationThresholdPolicy<TCase,TOutput>` applies an
`EvaluationThresholdEvaluator` to one named successful run evaluation.

The threshold evaluator supports:

- required or optional numeric minima;
- required or optional numeric maxima;
- required or optional boolean equality;
- structured per-threshold outcomes;
- missing/invalid required evidence as `Inconclusive` by default;
- explicit pessimistic treatment as `Failed`.

Optional thresholds permit a conditionally emitted metric to be absent. If an optional metric is
present but invalid or violates its bound, it still affects the decision.

## Binary Statistical Policy

`ExperimentBinarySuccessPolicy<TCase,TOutput>` evaluates a required boolean item metric with an
uncorrected Wilson score interval.

The caller explicitly supplies:

- required success rate;
- minimum effective sample count;
- one-sided confidence level;
- unknown-sample treatment.

Default item treatment is:

| Item outcome | Treatment |
|---|---|
| `Succeeded` with a valid boolean metric | Denominator success or failure. |
| `ExecutionFailed` | Denominator failure. |
| `TimedOut` | Denominator failure. |
| Task-originated `Canceled` | Denominator failure. |
| `EvaluationFailed`, missing/invalid metric, or failed metric diagnostics | Exclusion that forces `Inconclusive`. |

`ExperimentUnknownSampleTreatment.CountAsFailure` is the explicit pessimistic alternative.

The decision rule is:

```text
Passed         one-sided lower Wilson bound >= required success rate
Failed         one-sided upper Wilson bound < required success rate
Inconclusive   otherwise, below minimum sample count, or unknown evidence remains
```

Evidence reports trial count, operational attempt count, effective sample count, successes,
failures, execution failures, exclusions, status counts, estimate, bounds, confidence level,
threshold, and interval method. Each bound uses the configured one-sided confidence level. Taken
together, the lower and upper bounds have two-sided coverage `2 × confidenceLevel - 1`.

Retries only increase `AttemptCount`; they never increase `TotalTrialCount` or the statistical
denominator.

## Overall Decision

Required policies reduce deterministically:

1. Any required `Failed` policy makes the run `Failed`.
2. Otherwise, any required `Inconclusive` policy makes the run `Inconclusive`.
3. Otherwise, every required policy passed and the run is `Passed`.
4. No required policies produces `NotEvaluated`.

Optional policies remain in `PolicyResults` but do not affect the run decision. A policy exception
is represented as `Inconclusive` with a structured `PolicyFailed` failure; it is not fabricated into
a quality failure.

## EvaluationQualityGate Reuse

`EvaluationQualityGate` and experiment threshold policies share
`EvaluationThresholdEvaluator`. Use `Evaluate(...)` for structured non-throwing decisions and
`Assert(...)` as the CI-oriented throwing adapter.

Required missing or invalid metrics no longer pass silently:

```csharp
var gate = new EvaluationQualityGate()
    .RequireBoolean("required_metric", expected: true)
    .OptionalBoolean("conditional_metric", expected: true);

EvaluationThresholdResult result = gate.Evaluate(evaluation);
gate.Assert(evaluation);
```

`Assert(...)` throws for `Failed` or `Inconclusive`; an optional missing metric does not block a
passing decision.

## JSON Artifact

`ExperimentJsonArtifactWriter` writes schema version 2 with fixed Needlr-owned property ordering:

- source/trial item order;
- complete attempt history and delay-before-next-attempt values;
- normalized item metrics;
- ordered run-evaluation results;
- ordered policy results and structured evidence;
- the overall run decision;
- structured failures without raw exceptions or stack traces.

For Native AOT, pass caller-generated metadata:

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
AOT path. Caller-owned payload schemas remain caller responsibility. Determinism applies to the
Needlr envelope; it does not claim RFC 8785 cryptographic canonicalization.

## Provider Convergence Roadmap

The scheduler and quality core are complete, but provider lifecycle and publication adapters remain
separate reviewed work:

- [#49](https://github.com/ncosentino/needlr/issues/49) adds per-trial provider lifecycle scopes.
- [#50](https://github.com/ncosentino/needlr/issues/50) adds result sinks and publication outcomes.
- [#51](https://github.com/ncosentino/needlr/issues/51),
  [#52](https://github.com/ncosentino/needlr/issues/52), and
  [#53](https://github.com/ncosentino/needlr/issues/53) connect the runner to existing Langfuse
  dataset, trace, score, identity, resilience, and disabled-mode primitives.
- [#54](https://github.com/ncosentino/needlr/issues/54) adds MEAI Reporting afterward as the
  second-provider proof.

Langfuse convergence intentionally precedes MEAI Reporting because Langfuse support motivated the
runner and its lower-level prerequisites are already complete. No additional scheduler or policy
expansion is planned before that convergence.

## Runnable Example

Run the credential-free Phase 2 example:

```bash
dotnet run --project src/Examples/AgentFramework/ExperimentRunnerApp
```

It uses seeded stochastic trials, a scripted retry, mixed terminal outcomes, a shared limiter, run
evaluation, deterministic and Wilson-bound policies, and an AOT-safe schema-v2 JSON artifact under
the system temporary directory.
