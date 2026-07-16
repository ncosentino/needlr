# Experiment Runner

Run finite evaluation cases and repeated trials with bounded concurrency, delayed retries,
per-trial provider scopes, run-level measurement, structured quality policies, and deterministic
JSON—without coupling the experiment to a specific agent loop, test framework, dataset host, or
observability provider.

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
ExperimentRunOutcome<MyCase, MyOutput> outcome =
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

ExperimentRunResult<MyCase, MyOutput> result = outcome.Result;
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
trial index, one-based attempt number, and exact-type adapter features exposed by item scopes.

## Validation Before Execution

The runner rejects invalid static configuration before starting any item:

- blank experiment, run, source, case, run-evaluator, policy, or item-scope names;
- duplicate case IDs, run-evaluator names, policy names, or item-scope names using ordinal
  comparison;
- non-positive trial counts or `MaxConcurrency`;
- non-positive retry-policy attempt limits;
- invalid attempt or item-scope cleanup timeouts;
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

- acquires one `IExperimentConcurrencyLimiter` lease before first-attempt scope entry and reuses
  that lease for the attempt;
- acquires one lease around each later attempt;
- awaits acquisition with the caller token;
- disposes the lease when scope entry or the attempt completes;
- releases the lease before computing or waiting through a retry delay;
- never disposes the caller-owned limiter.

The built-in `ExperimentConcurrencyLimiter` is semaphore-backed and suitable for DI registration.
Alternate implementations can adapt provider quotas or resource-aware admission logic.

## Per-Trial Item Scopes

`ExperimentDefinition<TCase,TOutput>.ItemScopes` adds provider lifecycle without giving providers
ownership of scheduling, retries, evaluation, or quality policy.

For every statistical trial, the runner:

1. enters each `IExperimentItemScopeProvider<TCase,TOutput>` once in registration order;
2. snapshots exact-type scope features into `ExperimentItemFeatureCollection`;
3. activates entered scopes in registration order around every task attempt;
4. deactivates them in reverse order before a retry delay;
5. reactivates them around the single item evaluator after terminal execution success;
6. sends every terminal quality status to `CompleteAsync` in registration order;
7. disposes scopes in reverse order and records completion or disposal failure structurally.

The scope object remains alive across delayed retries, but no activation handle, worker slot, or
shared-limiter lease remains held during the delay. `EnterAsync` receives only the caller token and
runs outside `AttemptTimeout`; it executes after shared admission so provider setup participates in
the caller-owned concurrency boundary.

Tasks and evaluators retrieve adapter features by exact registered type:

```csharp
MyProviderFeature feature =
    context.Features.GetRequired<MyProviderFeature>();
```

A feature object must remain valid across repeated activation/deactivation cycles. Scope
implementations snapshot their feature dictionary during entry, so later mutation does not change
the trial context. Registering the same exact feature type from a later scope fails that later
scope deterministically; the first registration remains available.

`ExperimentItemScopeFailureMode.BestEffort` records entry or activation failure as
`ExperimentPublicationOperationStatus.Failed` while quality processing continues.
`ExecutionPrerequisite` stops the next task attempt and produces `PrerequisiteFailed` with no
fabricated attempt. Prerequisite failures are unknown statistical samples by default; the binary
policy excludes them unless `ExperimentUnknownSampleTreatment.CountAsFailure` is selected.

`IsRequired` does not change item quality. It is retained on each
`ExperimentItemPublicationResult` for aggregate publication health in the result-sink phase.
Provider correlations use structured namespace/name/value triples and are copied into both the
provider result and the item aggregate.

Normal scope teardown is `CompleteAsync` then `DisposeAsync`. Caller cancellation invokes
`AbortAsync` then `DisposeAsync` in reverse scope order for every entered scope whose completion has
not started. Once `CompleteAsync` begins, that method owns cancellation-safe termination and the
runner does not also invoke `AbortAsync`. Completion, abort, and disposal run without ambient
activation and must use scope-owned state. `ExperimentRunOptions.ItemScopeCleanupTimeout` bounds
cancellation cleanup and terminal disposal; it defaults to 30 seconds.

## Final Result Sinks

`ExperimentDefinition<TCase,TOutput>.Sinks` publishes the completed canonical quality result without
giving providers ownership of execution or quality policy.

Each named `IExperimentResultSink<TCase,TOutput>`:

- receives the same `ExperimentRunResult<TCase,TOutput>` reference after run evaluation and policy
  reduction finish;
- runs sequentially in registration order;
- declares whether publication is required;
- returns `Succeeded`, `Failed`, or `NotAttempted`;
- owns provider retry only when its operation is provably idempotent.

A thrown exception, `null`, or malformed sink result becomes a structured `ResultSinkFailed`
operation and does not suppress later sinks. Caller-token cancellation propagates exactly, skips
later sinks, and produces no outcome.

`IExperimentRunner.RunAsync` returns `ExperimentRunOutcome<TCase,TOutput>`:

| Publication status | Meaning |
|---|---|
| `NotRequested` | No item scope or final sink attempted publication. |
| `Succeeded` | Every attempted publication succeeded. |
| `PartiallyFailed` | At least one optional publication failed and no required publication failed. |
| `Failed` | At least one required publication failed. |

The aggregate includes both `Result.Items[*].Publications` and final `SinkResults`.
`outcome.PublicationStatus` never changes `outcome.Result.Decision`.

Needlr-owned collections are read-only snapshots. Caller-owned case/output values and mutable MEAI
evaluation objects cannot be deeply frozen; sinks must treat the entire result as read-only.

## Cancellation and Timeouts

Caller cancellation:

- stops ready-channel admission, limiter waits, active tasks, retry delays, item evaluation, run
  evaluation, policies, and final result sinks;
- wins over simultaneous timeout or execution failure;
- is rethrown from `RunAsync` with the original caller token;
- aborts every entered incomplete item scope and disposes scopes in reverse order within the
  configured cleanup timeout;
- produces no completed run outcome.

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
| `EvaluationFailed`, `PrerequisiteFailed`, missing/invalid metric, or failed metric diagnostics | Exclusion that forces `Inconclusive`. |

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

`ExperimentJsonArtifactWriter` writes a schema-version-4 outcome envelope with fixed Needlr-owned
property ordering:

- the unchanged schema-version-3 canonical quality result;
- source/trial item order;
- complete attempt history and delay-before-next-attempt values;
- normalized item metrics;
- namespaced item correlations and ordered item-scope publication results;
- ordered run-evaluation results;
- ordered policy results and structured evidence;
- the overall run decision;
- aggregate publication status and ordered final sink results;
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
    outcome,
    MyJsonContext.Default.MyCase,
    MyJsonContext.Default.MyOutput,
    cancellationToken);
```

The `JsonTypeInfo<TCase>` / `JsonTypeInfo<TOutput>` overload is the dependable trimmed and Native
AOT path. Caller-owned payload schemas remain caller responsibility. Determinism applies to the
Needlr envelope; it does not claim RFC 8785 cryptographic canonicalization.

## Provider Convergence Roadmap

The scheduler, quality core, lifecycle and publication seams, and full Langfuse source/scope/sink
convergence are complete. The remaining second-provider proof is:

- [#51](https://github.com/ncosentino/needlr/issues/51) provides validated paginated Langfuse
  dataset reads and `LangfuseDatasetCaseSource<TCase>` for latest or timestamped hosted sources.
- [#52](https://github.com/ncosentino/needlr/issues/52) creates one reactivatable Langfuse trace and
  at most one hosted dataset-run-item link per statistical trial.
- [#53](https://github.com/ncosentino/needlr/issues/53) projects canonical item/run measurements and
  the optional canonical decision through the final Langfuse result sink.
- [#54](https://github.com/ncosentino/needlr/issues/54) adds MEAI Reporting afterward as the
  second-provider proof.

Langfuse convergence intentionally precedes MEAI Reporting because Langfuse support motivated the
runner and its lower-level prerequisites are already complete. No additional scheduler or policy
expansion is planned before that convergence.

## Runnable Example

Run the credential-free Phase 3B example:

```bash
dotnet run --project src/Examples/AgentFramework/ExperimentRunnerApp
```

It uses seeded stochastic trials, a scripted retry, mixed terminal outcomes, a shared limiter, run
evaluation, deterministic and Wilson-bound policies, a credential-free final sink, and an AOT-safe
schema-v4 outcome artifact under the system temporary directory.
