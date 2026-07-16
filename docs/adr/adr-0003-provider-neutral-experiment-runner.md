---
title: "ADR-0003: Provider-neutral experiment runner over MEAI Evaluation"
status: "Accepted"
date: "2026-07-12"
authors: "Nick Cosentino"
tags: ["architecture", "decision", "agent-framework", "evaluation", "experiments", "meai", "langfuse"]
supersedes: ""
superseded_by: ""
---

## Status

Accepted - proceed through the separately reviewed implementation phases below.

This ADR was the design deliverable for [issue #36](https://github.com/ncosentino/needlr/issues/36).
Phase 1 was delivered by [issue #43](https://github.com/ncosentino/needlr/issues/43).
Phase 2 was delivered by [issue #45](https://github.com/ncosentino/needlr/issues/45).
Phase 3A was delivered by [issue #49](https://github.com/ncosentino/needlr/issues/49).
Phase 3B was delivered by [issue #50](https://github.com/ncosentino/needlr/issues/50).
Phase 4A was delivered by [issue #51](https://github.com/ncosentino/needlr/issues/51).
Phase 4B was delivered by [issue #52](https://github.com/ncosentino/needlr/issues/52).
Phase 4C was delivered by [issue #53](https://github.com/ncosentino/needlr/issues/53).
Phase 5 was delivered by [issue #54](https://github.com/ncosentino/needlr/issues/54).

The post-Phase-2 convergence audit found that the provider-neutral scheduler and the existing
Langfuse primitives are complementary, but the item-lifecycle and result-sink seams proposed by this
ADR were still missing. That left Langfuse documentation relying on a caller-owned item loop instead
of the generic runner.

The separately reviewed implementation sequence is complete. Further scheduler, retry, evaluator,
statistical-policy, or provider expansion requires a separate design and issue.

## Decision

Needlr should add a provider-neutral experiment runner that owns:

- finite case materialization and validation;
- expansion of cases into statistically meaningful trials;
- bounded item scheduling;
- execution attempts, timeouts, and retries;
- whole-item failure isolation;
- deterministic result ordering;
- run-level evaluation and policy decisions;
- structured publication outcomes across independent sinks.

Needlr should not own:

- MEAI evaluator composition;
- MEAI response caches;
- MEAI evaluation-result stores;
- MEAI report generation;
- Langfuse dataset, trace, score, or comparison semantics;
- test-framework fixtures or process-global coordination.

`Microsoft.Extensions.AI.Evaluation.EvaluationResult` remains the item- and run-metric payload because Needlr's evaluation package already uses the MEAI core abstractions directly. MEAI Reporting types such as `ReportingConfiguration`, `ScenarioRun`, and `ScenarioRunResult` do not become Needlr's experiment data model.

In this ADR, provider-neutral means neutral across execution engines, dataset hosts, observability platforms, and publication targets. It does not mean evaluation-abstraction neutral: MEAI core is the deliberate metric lingua franca for Needlr's evaluation package.

The runner belongs in the existing `NexusLabs.Needlr.AgentFramework.Evaluation` package under an
`Experiments` namespace. Langfuse-specific adapters remain in
`NexusLabs.Needlr.AgentFramework.Langfuse`; MEAI Reporting-specific dependencies remain in
`NexusLabs.Needlr.AgentFramework.Evaluation.Reporting`.

## Context

Needlr already provides:

- native MEAI evaluation inputs and evaluators;
- deterministic and LLM-judged agent evaluators;
- a single-result `EvaluationQualityGate`;
- response capture and replay;
- agent and pipeline scenario runners;
- Langfuse scenario, dataset-run, and score primitives;
- bounded Langfuse shutdown, caller-cancellation preservation, isolated trace context, and a complete non-owning `ILangfuseClient`.

It does not provide collection-level experiment orchestration. Consumers still have to independently implement:

- bounded concurrency across cases;
- failure isolation between cases;
- retries that remain distinct from statistical trials;
- run-level aggregates and acceptance policies;
- one structured result that retains failures;
- publication to multiple reporting or observability systems.

The existing `AgentScenarioRunner` and `PipelineScenarioRunner` are not the missing abstraction. They each execute one Needlr-specific scenario shape and combine execution with verification. An experiment runner must remain independent of a particular agent loop, pipeline, test framework, or verification style.

### Verified external boundaries

The repository currently pins MEAI Evaluation and Reporting `10.5.0`. The latest stable Reporting package reviewed during this spike was `10.7.0`; its documented responsibility remains response caching, result storage, and reporting rather than collection scheduling.

At the pinned version:

- `ReportingConfiguration` configures evaluators, judge chat configuration, response caching, result storage, execution identity, interpretation, and tags.
- `ScenarioRun` represents one scenario iteration, invokes a `CompositeEvaluator`, and persists its result when disposed.
- `CompositeEvaluator` runs item evaluators concurrently and converts evaluator exceptions into metric diagnostics.
- MEAI does not schedule a collection of scenarios, bound item concurrency, model execution attempts, apply retries, or run aggregate evaluators.

Langfuse's current Python and JavaScript/TypeScript experiment runners provide concurrent item execution, item and run evaluators, tracing, error isolation, and hosted-dataset integration. They are useful validation that the orchestration problem is real, but they are not a suitable canonical model for Needlr:

- both SDKs currently default to a concurrency of 50;
- their scheduling implementations differ;
- failed task items are logged and omitted from the `itemResults` supplied to run evaluators;
- they do not expose a first-class retry/attempt model;
- local datasets and hosted datasets have different publication behavior.

Needlr should not clone those provider-specific semantics, especially the omission of failed items from aggregate inputs.

### Decision constraints

- Needlr is alpha. Prefer intentional API correction over compatibility shims.
- Public results must be structured and machine-actionable.
- A human-readable formatter is an adapter, not the canonical result.
- No static singleton holders or process-global mutable coordination.
- No xUnit, NUnit, MSTest, or test-runner types in the core API.
- No universal concurrency default derived from one provider or workload.
- Provider publication failure must not redefine evaluation quality.
- Implementation must use strict TDD and include runnable, credential-free examples.

## Independent evaluation shapes

The runner is justified only if one model supports materially different evaluation shapes.

### Shape 1: finite deterministic regression suite

Example:

- 200 fixed agent scenarios;
- one trial per case;
- no retry unless a case explicitly opts into a transient-failure policy;
- deterministic trajectory, termination, and token-budget evaluators;
- acceptance requires every required metric and no failed execution.

This shape needs:

- stable source ordering;
- bounded concurrency;
- exact failure accounting;
- deterministic pass/fail policy;
- a CI adapter that can translate the structured decision to a process exit code.

### Shape 2: repeated stochastic trials

Example:

- 40 logical scenarios;
- 10 independent trials per scenario;
- an LLM or agent execution whose output can vary;
- run evaluators that calculate success rate, execution-failure rate, latency, cost, and a confidence interval;
- acceptance that can return passed, failed, or inconclusive.

This shape needs:

- a strict distinction between a trial and an operational retry;
- sample and exclusion counts;
- all failed and timed-out items retained in aggregate inputs;
- an explicit response-cache policy so repeated trials are not accidentally replayed from one cached response;
- statistical policies that report uncertainty rather than only a mean.

These shapes share scheduling, attempts, result ordering, failure isolation, and publication. They differ at the run-evaluation and policy layers, which supports a common runner with composable policies.

## Terminology

| Term | Meaning |
|---|---|
| Case | One logical input and its caller-defined data. |
| Trial | One statistically independent scheduled execution of a case. |
| Item | The runner's work unit for one case and one trial index. |
| Attempt | One operational execution try for an item. Retries add attempts, not trials. |
| Item evaluation | Metrics calculated from one terminal successful item output. |
| Run evaluation | Metrics calculated across every item result, including failures. |
| Policy | A structured acceptance decision derived from item and run metrics. |
| Sink | An independent publisher or artifact writer that receives a completed run result. |

The identity of an item is `(CaseId, TrialIndex)`. The identity of an attempt is `(CaseId, TrialIndex, AttemptNumber)`.

## Options considered

| Path | Benefits | Costs and risks | Migration and future compatibility |
|---|---|---|---|
| A. Provider-neutral Needlr runner | Solves scheduling, attempts, failures, aggregation, and publication once; supports local and hosted data; retains a single structured result; test-framework independent. | Medium public API cost; Needlr must maintain concurrency, cancellation, and failure semantics. | Best isolation from MEAI Reporting and provider changes. Adapters can evolve without changing experiment definitions. |
| B. Thin MEAI Reporting extensions | Lowest immediate implementation and API cost; directly follows Microsoft types. | Does not solve collection scheduling, retries, aggregate policy, or sink fan-out; every consumer still builds a runner. | Low library maintenance but high consumer fragmentation. A later canonical runner becomes harder to adopt. |
| C. Langfuse-specific runner | Fastest path to familiarity for Langfuse Python/JS users; direct hosted-dataset integration. | Provider lock-in; duplicates changing SDK behavior; failed-item semantics are unsuitable; creates a second Needlr evaluation model. | Highest migration risk if Langfuse changes or MEAI later adds orchestration. Non-Langfuse users still need another solution. |

### Option A: provider-neutral Needlr runner

**Pros**

- The missing behavior is general across deterministic and stochastic workloads.
- Failure, timeout, retry, and ordering semantics become consistent.
- Providers cannot silently change the quality verdict.
- Local data remains first-class.
- MEAI and Langfuse each retain ownership of their established capabilities.

**Cons**

- The scheduler and result model become long-lived Needlr API.
- Statistical policy design must avoid false universality.
- Adapter lifecycle hooks require careful async-context testing.

### Option B: thin MEAI Reporting extensions

**Pros**

- Minimal code and dependency risk.
- MEAI remains the visible user model.

**Cons**

- `ReportingConfiguration` and `ScenarioRun` are not collection schedulers.
- Consumers still duplicate concurrency, retries, partial-failure handling, and aggregate decisions.
- Provider publication and evaluation quality remain easy to conflate.

This path may produce small convenience adapters, but it is rejected as the end state for issue #36.

### Option C: Langfuse-specific runner

**Pros**

- Direct parity with Langfuse terminology and hosted comparison views.
- Less adapter work for Langfuse-only consumers.

**Cons**

- Concurrency and failure semantics would inherit provider behavior.
- Local-only and alternate-provider consumers would receive a second-class API.
- Current Langfuse SDKs omit failed tasks from aggregate inputs.
- Retries, statistical trials, and MEAI Reporting would remain awkward additions.

This path is avoided.

## Proposed architecture

```text
Experiment definition
  |
  +-- finite case source
  +-- item task
  +-- optional item evaluator
  +-- run evaluators
  +-- run policies
  +-- item lifecycle adapters
  +-- result sinks
  |
Experiment runner
  |
  +-- materialize and validate cases
  +-- expand cases into ordered trials
  +-- schedule bounded attempts
  +-- isolate item failures
  +-- evaluate successful items
  +-- evaluate the full run
  +-- calculate the quality decision
  +-- publish through independent sinks
  |
Experiment run outcome
  +-- canonical run result
  +-- independent publication results
```

### Proposed definition and execution surface

The following is the intended shape, not a finalized signature:

```csharp
public interface IExperimentRunner
{
    ValueTask<ExperimentRunOutcome<TOutput>> RunAsync<TCase, TOutput>(
        ExperimentDefinition<TCase, TOutput> definition,
        ExperimentRunOptions options,
        CancellationToken cancellationToken = default);
}

public sealed record ExperimentDefinition<TCase, TOutput>
{
    public required string Name { get; init; }
    public required IExperimentCaseSource<TCase> CaseSource { get; init; }
    public required ExperimentTask<TCase, TOutput> Task { get; init; }
    public IExperimentItemEvaluator<TCase, TOutput>? ItemEvaluator { get; init; }
    public IReadOnlyList<IExperimentRunEvaluator<TCase, TOutput>> RunEvaluators { get; init; }
    public IReadOnlyList<IExperimentRunPolicy<TCase, TOutput>> Policies { get; init; }
    public IReadOnlyList<IExperimentItemScopeProvider<TCase, TOutput>> ItemScopes { get; init; }
    public IReadOnlyList<IExperimentResultSink<TOutput>> Sinks { get; init; }
}

public sealed record ExperimentRunOptions
{
    public required string RunId { get; init; }
    public required int MaxConcurrency { get; init; }
    public TimeSpan? AttemptTimeout { get; init; }
    public IExperimentRetryPolicy RetryPolicy { get; init; }
    public IExperimentConcurrencyLimiter? SharedLimiter { get; init; }
}
```

The implementation may refine names or generic placement, but it must preserve these boundaries:

- one runner service with injected dependencies;
- a finite case source;
- an attempt-level task callback;
- item evaluation after terminal execution success;
- full-run evaluators and policies;
- item-lifecycle scopes for provider context;
- independent final-result sinks.

Interfaces are limited to genuine extension points. Result types are sealed data types rather than inheritance hierarchies.

There is at most one item-evaluation producer. A caller that needs multiple evaluators composes them behind that one interface. A MEAI Reporting adapter supplies the item evaluator and uses its paired item scope to reach the same `ScenarioRun`; it does not add a second hidden evaluation stage.

### Case and source model

The minimum case model is:

```text
ExperimentCase<TCase>
  Id                 required, unique within the materialized source
  Value              caller-defined input, expected output, and metadata
  TrialCount         positive; defaults to one
  Tags               optional string tags

ExperimentCaseSourceResult<TCase>
  Source             provider-neutral name/id/version reference
  Cases              finite ordered case list
```

The core does not define separate generic input, expected-output, and metadata types. Callers model those together in `TCase`, which avoids a growing generic arity and lets hosted adapters preserve provider references in an adapter-owned case type.

The source is fully materialized before execution. The runner validates:

- non-empty run and case identifiers;
- unique case identifiers;
- positive trial counts;
- positive `MaxConcurrency`;
- representable total item count.

Source or validation failures occur before any item starts and are thrown to the caller.

### Attempt and item result model

```text
ExperimentAttemptResult
  AttemptNumber       one-based
  Status              Succeeded | Failed | TimedOut | Canceled
  StartedAt
  Duration
  Failure             structured failure when not successful

ExperimentItemResult<TOutput>
  Sequence            stable source-order/trial-order index
  CaseId
  TrialIndex          one-based statistical sample index
  Status              Succeeded | ExecutionFailed | TimedOut |
                      Canceled | EvaluationFailed | PrerequisiteFailed
  Attempts            every operational attempt in order
  Output              terminal successful output, when available
  Evaluation          MEAI EvaluationResult, when available
  Failure             terminal structured failure, when present
  Correlations        optional namespaced string identifiers for adapters
  Publications        item-scope publication results
```

`ExperimentFailure` is machine-actionable:

```text
ExperimentFailure
  Code
  Stage               Execution | ItemEvaluation | RunEvaluation |
                      Policy | Publication
  ExceptionType
  Message
  IsRetryable
```

The canonical result does not serialize a raw `Exception` or stack trace by default. Isolated
failures retain stable machine-actionable fields without making provider-specific exception types
part of the result contract.

An item-scope publication result is separate from item quality:

```text
ExperimentItemPublicationResult
  Name
  IsRequired
  Status              Succeeded | Failed | NotAttempted
  Correlations
  Failure
```

### Run evaluation and policy model

```text
ExperimentRunEvaluationResult
  Name
  Status              Succeeded | Failed
  Metrics             MEAI EvaluationResult when successful
  Failure             structured failure when failed

ExperimentPolicyResult
  Name
  Kind                Deterministic | Statistical
  IsRequired
  Decision            Passed | Failed | Inconclusive
  Evidence            structured policy evidence containing MEAI metrics,
                      status counts, thresholds, bounds, and exclusions
  Failure             structured failure when policy execution failed

ExperimentRunResult<TOutput>
  SchemaVersion
  RunId
  ExperimentName
  Source
  StartedAt
  Duration
  Items
  RunEvaluations
  PolicyResults
  Decision            Passed | Failed | Inconclusive | NotEvaluated
```

The overall decision is reduced deterministically:

1. Any required failed policy makes the run `Failed`.
2. Otherwise, any required inconclusive policy makes the run `Inconclusive`.
3. Otherwise, all required policies passing makes the run `Passed`.
4. No required policies makes the run `NotEvaluated`.

A policy exception does not become a fabricated quality failure. It produces an inconclusive policy result with a structured failure.

### Publication model

Sinks receive a read-only snapshot of `ExperimentRunResult<TOutput>` and return structured sink results. The runner then returns:

```text
ExperimentSinkResult
  Name
  IsRequired
  Status              ExperimentPublicationOperationStatus:
                      Succeeded | Failed | NotAttempted
  Failure

ExperimentRunOutcome<TOutput>
  SchemaVersion       4
  Result              canonical ExperimentRunResult<TOutput>
  PublicationStatus   ExperimentPublicationStatus:
                      NotRequested | Succeeded | PartiallyFailed | Failed
  SinkResults
```

This two-stage model prevents publication outcomes from being folded back into the quality decision. Sinks receive a read-only snapshot contract. The implementation copies all Needlr-owned collections and normalizes MEAI metrics before fan-out; arbitrary caller-owned `TOutput` values cannot be deeply frozen and are read-only by contract.

Required sink failure changes `PublicationStatus`, not `Result.Decision`. A CI adapter may independently require both a passing quality decision and successful required publication.

`PublicationStatus` aggregates both item-scope publication results and final sink results:

- `NotRequested` when no scope or sink attempted publication;
- `Succeeded` when every attempted publication succeeded;
- `PartiallyFailed` when only optional publications failed;
- `Failed` when any required publication failed.

### Item lifecycle adapters

Some providers need a scope around the entire item rather than only a final sink:

- MEAI Reporting creates a `ScenarioRun` before model execution when response caching is used.
- Langfuse creates and activates one scenario trace for the trial and links it to a hosted dataset run.

The runner therefore needs an item-scope extension point with these constraints:

- scopes wrap one trial, not each retry attempt;
- the runner still owns admission, attempts, timeout, retry, and decision semantics;
- scopes can expose adapter-specific execution context to the task;
- the runner invokes exactly one configured item evaluator after terminal execution success;
- scopes can provide state to that evaluator but do not independently produce a second evaluation;
- the runner builds the execution/evaluation portion, notifies scopes, disposes them, and then finalizes the item result with their publication results;
- scopes receive an explicit abort path for caller cancellation before disposal;
- provider failures are publication failures unless the caller explicitly makes them execution prerequisites;
- multiple scopes enter in registration order and exit in reverse order;
- no scope may rely on static or process-global mutable state.

The finalized Phase 3A contract separates the provider factory from its per-trial scope:

```text
IExperimentItemScopeProvider<TCase,TOutput>
  Name
  IsRequired
  FailureMode         BestEffort | ExecutionPrerequisite
  EnterAsync          once per statistical trial

IExperimentItemScope<TCase,TOutput>
  Features            exact-Type adapter feature map
  Activate            context-restoration handle per attempt/evaluator
  CompleteAsync       terminal quality notification and publication result
  AbortAsync          caller-cancellation notification
  DisposeAsync        final cleanup
```

Entry runs under caller cancellation and shared admission but outside the per-attempt timeout.
Activation handles nest in registration order and unwind in reverse order. Completion, abort, and scope disposal run without ambient activation and use scope-owned state.
Caller cancellation aborts only scopes whose completion has not started; a started completion owns
cancellation-safe termination and is never followed by abort. `ItemScopeCleanupTimeout` bounds
cancellation cleanup and terminal disposal.

`BestEffort` scope failure never changes item quality. `ExecutionPrerequisite` applies only before a
task attempt starts; a failed prerequisite produces `PrerequisiteFailed` without inventing an
attempt. Completion or disposal failures remain publication failures after quality is known.

The item lifecycle is:

1. Enter item scopes and collect their scoped features.
2. Execute attempts under runner-owned timeout and retry rules.
3. On terminal execution success, invoke the one item evaluator.
4. Build the execution/evaluation portion, including `EvaluationFailed` when the evaluator throws.
5. Notify scopes of that read-only quality outcome and collect structured publication results.
6. Dispose scopes in reverse order; disposal failure updates only publication status.
7. Finalize the item result with correlations and publication results.

The MEAI Reporting adapter implements a coordinated scope/evaluator pair: the scope owns `ScenarioRun`, and the evaluator is the only component that calls `ScenarioRun.EvaluateAsync(...)`.

Issues #33 and #34 established the context-safe Langfuse callback and structured link result used
to finalize this provider-neutral contract. Langfuse types remain outside the core package.

## Execution semantics

### Materialization and ordering

- Materialize the finite source before scheduling.
- Expand cases in source order, then ascending `TrialIndex`.
- Assign a stable `Sequence` before execution.
- Return item results in `Sequence` order regardless of completion order.
- Invoke run evaluators, policies, scopes, and sinks in their registration order unless an API explicitly states otherwise.

### Bounded concurrency

- `MaxConcurrency` is required and must be positive.
- It bounds active execution attempts, not total cases and not queued retry delays.
- Do not create one task per dataset item and place all tasks behind a semaphore.
- Use a bounded worker/scheduler design whose pending work is data, not an unbounded task collection.
- A retry delay holds neither a local execution permit nor a shared permit.
- A retry is re-enqueued with a scheduled ready time; a worker does not sleep in place while holding a worker slot.

An optional caller-owned `IExperimentConcurrencyLimiter` provides a second admission boundary shared by simultaneous runs. The runner:

- awaits it with the caller token;
- acquires it per attempt;
- releases it before retry delay;
- never disposes it;
- does not create a default process-global limiter.

The built-in limiter can be registered and shared through DI. Alternate implementations may adapt `System.Threading.RateLimiting`, provider quotas, or resource-aware admission policies.

### Cancellation

- Caller cancellation stops new admission immediately.
- Limiter waits, active attempts, evaluator calls, and retry delays receive cancellation.
- If the caller token is requested, caller cancellation wins over a simultaneous timeout.
- `RunAsync` rethrows `OperationCanceledException`; it does not convert cancellation into an item failure or a completed run result.
- Final run evaluators, policies, and sinks do not run after caller cancellation.
- Entered item scopes receive an abort notification and are disposed through bounded cleanup.
- An aborted scope must not publish a completed evaluation record for an incomplete item.

Completed provider side effects may already exist when cancellation occurs. A resumable partial-run artifact is deferred rather than hidden behind a success-shaped result.

### Timeouts

- Timeout is per attempt.
- The task receives a linked token combining caller cancellation and attempt deadline.
- Caller-token cancellation is rethrown.
- Deadline cancellation while the caller token remains active produces `TimedOut`.
- A task-originated cancellation with neither token requested produces `Canceled`.
- Timeout is retryable only when the configured retry policy says so.

### Retries

- Default is one attempt and no retry.
- Retries apply only to execution failure, timeout, or task-originated cancellation selected by policy.
- Caller cancellation is never retried.
- Item evaluation, run evaluation, policy, scope, and sink failures never rerun the agent task.
- Every attempt and retry delay is recorded.
- Retry attempts do not increase the statistical sample count.
- Hidden random jitter is prohibited. Any jitter policy must be explicit and reproducibly seeded.

### Item evaluation

- Item evaluation runs once after terminal execution success.
- It receives the case, trial identity, output, and attempt history.
- It returns a MEAI `EvaluationResult`.
- Evaluator failure preserves the successful task output and marks the item `EvaluationFailed`.
- The definition has at most one item evaluator. Multiple evaluator implementations must be explicitly composed behind it.
- A MEAI Reporting adapter occupies that one evaluator slot and delegates to `ScenarioRun` and its `CompositeEvaluator`.
- The runner never invokes both a direct item evaluator and a hidden MEAI evaluator for the same item.

### Whole-item failure isolation

- One item failure does not cancel siblings.
- Failed, timed-out, canceled, and evaluation-failed items remain in the run result.
- Run evaluators receive every item, not only successes.
- Any exclusion must be explicit and reported with an exclusion count.
- Execution failures count in denominators by default.

### Run evaluators and policies

Run evaluators measure. Policies decide.

- Run evaluators receive the complete ordered item list.
- Run evaluator failures are isolated and represented structurally.
- Run evaluators execute sequentially in registration order initially; no hidden second concurrency policy is introduced.
- Policies consume item results and successful run-evaluation metrics.
- Policies cannot mutate measurements.
- Human-readable report rendering remains outside the policy result.

The existing `EvaluationQualityGate` is not duplicated. Phase 2 refactors its threshold checks into a shared structured result and keeps `Assert(...)` as a throwing adapter over that same evaluation.

Missing required metrics produce `Inconclusive` by default, or `Failed` when the policy explicitly selects pessimistic treatment. `Optional*` thresholds preserve conditionally emitted metric support. Updating `Assert(...)` to use required-metric semantics is an intentional alpha correction rather than a compatibility shim.

### Statistical policies

The runner does not claim one universal statistical method.

A statistical run evaluation must report at least:

- total trial count;
- successful sample count;
- execution-failure count;
- exclusion count;
- estimate;
- uncertainty measure or confidence interval;
- confidence level;
- baseline and paired delta when applicable.

The initial statistical vertical slice should support a binary success proportion with a documented confidence interval and a policy such as:

```text
Passed         lower confidence bound >= required threshold
Failed         upper confidence bound < required threshold
Inconclusive   otherwise, or minimum sample requirements are unmet
```

The default binary-policy status mapping is:

| Item state | Statistical treatment |
|---|---|
| `Succeeded` with a valid required boolean metric | Denominator success or failure according to the metric value. |
| `ExecutionFailed` | Denominator failure. |
| `TimedOut` | Denominator failure. |
| Task-originated `Canceled` | Denominator failure. |
| `EvaluationFailed`, `PrerequisiteFailed`, missing metric, or metric with failed diagnostics | Unknown sample; counted as an exclusion and forces `Inconclusive` unless the policy explicitly selects pessimistic failure treatment. |
| Explicit policy exclusion | Excluded with a structured reason and counted by status. |

Every statistical result reports counts by item status in addition to total success, failure, and exclusion counts. A policy cannot silently remove an item from its denominator.

Paired candidate/baseline comparisons, clustered bootstrap methods, continuous-metric policies, adaptive sampling, and power-analysis helpers are deferred extension points.

## MEAI Reporting adapter boundary

The MEAI Reporting adapter is responsible for:

1. Creating or receiving a run-specific `ReportingConfiguration`.
2. Mapping the Needlr run ID to MEAI `ExecutionName`.
3. Mapping case ID to `ScenarioName`.
4. Mapping trial index to `IterationName`.
5. Creating one `ScenarioRun` per statistical trial, not per retry attempt.
6. Exposing the scenario's wrapped `ChatConfiguration` to the task when response caching is desired.
7. Supplying the definition's single item evaluator, which calls `ScenarioRun.EvaluateAsync(...)` once after successful execution.
8. Mapping an evaluator exception to the runner's `EvaluationFailed` item status.
9. Disposing the `ScenarioRun` after the item result is built so the configured MEAI result store persists it.
10. Returning the `EvaluationResult` as the item's canonical metrics.

MEAI continues to own:

- `CompositeEvaluator`;
- judge chat configuration;
- response-cache providers and keys;
- evaluation-result stores;
- report writers and the `dotnet aieval` report workflow.

The adapter must make response reuse explicit for repeated trials:

| Mode | Behavior |
|---|---|
| `CaseAndTrialReplay` | Preserve MEAI's normal scenario/iteration cache identity across runs. Useful for deterministic replay when the complete request and model cache key also matches. |
| `FreshPerRun` | Add the Needlr run ID to cache identity so a new run produces new stochastic samples. |
| `Disabled` | Require a `ReportingConfiguration` without a response-cache provider. |

One `ScenarioRun` and cache identity spans all retry attempts for an item. A response cached before a downstream execution failure may therefore be replayed by the retry. This is intentional because a retry is not a new statistical sample. A caller that requires a fresh model response on every attempt must disable MEAI response caching for that experiment; finer per-attempt cache policy is deferred.

The runner does not generate MEAI reports and does not create fake `ScenarioRun` entries for aggregate metrics. Run-level metrics remain in the Needlr result and may be published by sinks.

A MEAI result-store write failure is publication failure. Normal scope disposal invokes
`ScenarioRun.DisposeAsync`; the provider-neutral scope manager converts a thrown store exception into
the final failed `ExperimentItemPublicationResult`. It does not trigger another model execution,
disappear into logging, or change the item's quality status. `ScenarioRun.DisposeAsync` accepts no
cancellation token, so a cleanup timeout can be reported while the provider store operation finishes
later.

## Langfuse adapter boundary

Langfuse integrates through three independent adapters.

### Hosted case source

The source:

- loads a finite hosted dataset selection;
- records dataset identity and selected version in the source reference;
- maps each hosted item to a Needlr case while preserving dataset-item identity;
- performs all remote loading before item execution starts.

Local case sources remain first-class and require no Langfuse dataset.

### Item scope

The scope:

- creates one context-safe scenario trace per trial;
- links that trace to a hosted dataset item at most once;
- keeps every retry attempt beneath the same trial trace;
- exposes trace correlation to the item result;
- restores the prior ambient activity for nested and parallel items;
- records link or trace-publication failure independently from item quality.

This adapter depends on issue #33 for context-safe item execution and issue #34 for structured run identity and link status.

### Result sink

The sink:

- projects item MEAI metrics to trace or observation scores;
- projects run-evaluation metrics to dataset-run scores when a dataset-run ID exists;
- records item-link, score, and run-score outcomes;
- treats disabled Langfuse mode as a structurally valid no-op publication result;
- does not choose concurrency, retry, timeout, or the quality verdict.

High-volume retry, idempotency, batching, and export-health behavior remains owned by issue #35.

Local experiments may publish traces and scores without a Langfuse dataset run. The Needlr run result remains complete even when the provider cannot create a comparison view.

## Sink fan-out

- Sinks receive the same read-only run snapshot, including item-scope publication results.
- Sinks execute in deterministic registration order.
- One sink failure does not suppress later sinks.
- Sinks declare whether publication is required.
- Generic automatic sink retry is not provided because publication operations may not be idempotent.
- Provider adapters own bounded retry only after they establish provider-specific idempotency.
- Evaluation decision and publication status remain separate at every layer.

Expected initial sinks:

- canonical JSON artifact;
- MEAI Reporting persistence adapter;
- Langfuse publication adapter.

Human-readable console, Markdown, HTML, and CI-comment rendering consume the canonical outcome rather than changing it.

## Compatibility and versioning

The API is new and alpha:

- no compatibility shim is required;
- breaking corrections should be made before stabilization;
- result types should be sealed, non-positional types so fields can be added without constructor churn;
- interfaces should exist only at extension points;
- the canonical result and canonical JSON projection include `SchemaVersion` from the first release;
- serialized enum values and required fields become versioned contracts;
- the JSON sink normalizes MEAI metrics into a Needlr-owned metric snapshot rather than blindly serializing the mutable `EvaluationResult` object graph;
- `SchemaVersion` governs the Needlr envelope and normalized metric projection, not arbitrary caller-owned `TCase` or `TOutput` payloads;
- Langfuse types do not appear in the core package;
- MEAI Reporting types appear only in its adapter surface;
- no raw exception object is part of the serialized contract;
- caller-defined `TCase` and `TOutput` serialization remains caller responsibility.

The implementation must revalidate against the then-current stable MEAI packages before coding. This ADR was verified against the repository's pinned `10.5.0` surface and the current stable `10.7.0` responsibility boundary.

## Phased implementation plan

No phase begins from this ADR alone. Each phase requires its own issue and reviewed pull request.

### Phase 1: core scheduler and canonical result — delivered by #43

Deliver:

- finite case source and validation;
- case-to-trial expansion;
- required bounded concurrency;
- caller cancellation and per-attempt timeout;
- one-attempt default;
- whole-item failure isolation;
- deterministic result ordering;
- canonical JSON sink with normalized metric snapshots;
- no provider adapter.

Required TDD:

- exact maximum active attempts;
- no unbounded task creation;
- deterministic ordering under out-of-order completion;
- duplicate-ID and invalid-option rejection before execution;
- failure isolation;
- caller cancellation precedence over timeout;
- task-originated cancellation classification;
- failed and timed-out items retained.
- JSON schema version and normalized metric shape.

Runnable example:

- credential-free deterministic fake agent;
- finite local cases;
- mixed success and failure;
- canonical JSON output;
- no test-framework dependency.

### Phase 2: retries, run evaluation, and policy — delivered by #45

Deliver:

- explicit retry policy and attempt history;
- run evaluators;
- deterministic and statistical policy results;
- structured quality decision;
- shared concurrency limiter;
- `EvaluationQualityGate` refactored to reuse structured threshold evaluation.

Required TDD:

- retry selection and exhaustion;
- cancellation during retry delay;
- retry delays holding no permits;
- delayed retries re-entering the scheduler without occupying workers;
- retries not counted as trials;
- evaluator failure not rerunning execution;
- all item statuses reaching run evaluators;
- run-evaluator failure isolation;
- deterministic passed/failed decisions;
- statistical passed/failed/inconclusive decisions;
- two concurrent runs sharing one limiter.

Runnable example:

- seeded stochastic binary outcomes;
- repeated trials;
- estimate, confidence interval, sample count, attempts, and decision;
- no credentials.

### Phase 3A: per-trial provider lifecycle scopes — #49

Deliver:

- provider-neutral item scopes configured on the experiment definition;
- one scope entry per statistical trial, not per retry attempt;
- scope reactivation around every attempt and item evaluation after delayed re-enqueue;
- typed adapter features available to task and evaluator contexts;
- namespaced item correlations and structured item publication outcomes;
- terminal completion notification, caller-cancellation abort, and reverse-order disposal.

Required TDD:

- one scope per trial across multiple attempts;
- scope reactivation after retry delay and worker changes;
- nested scope ordering and reverse disposal;
- the single item evaluator runs inside the same scope;
- all terminal item statuses reach scope completion;
- cancellation invokes abort and does not publish a completed item.

### Phase 3B: result sinks and publication outcomes — #50

Delivered by issue #50.

Deliver:

- deterministic result-sink fan-out;
- required and optional sink semantics;
- `ExperimentRunOutcome<TCase,TOutput>` containing the canonical result and publication outcomes;
- aggregate `NotRequested`, `Succeeded`, `PartiallyFailed`, or `Failed` publication status;
- item-scope and final-sink publication aggregation;
- an intentional alpha return-type correction for `IExperimentRunner.RunAsync`;
- schema-versioned outcome serialization.

Required TDD:

- deterministic sink order;
- required versus optional failure aggregation;
- one failing sink does not suppress another;
- publication status never changes the quality decision;
- caller cancellation skips final sinks;
- runs without scopes or sinks remain `NotRequested`.

### Phase 4A: Langfuse hosted case source — #51

Deliver:

- paginated read-side dataset and dataset-item APIs;
- latest and explicit version-timestamp selection;
- finite hosted-case materialization before execution;
- caller-defined mapping from hosted item data to `ExperimentCase<TCase>`;
- dataset identity and version in `ExperimentSourceReference`;
- coherent disabled behavior that cannot silently produce a passing empty run.

The hosted binding keeps the Langfuse dataset item id as `ExperimentCase<TCase>.Id`. Caller mapping
owns the case value, trial count, and tags, but cannot replace that provider identity. This lets the
next-phase item scope link a trial using only the provider-neutral case id while input, expected
output, metadata, and source trace/observation references remain available in the adapter-owned
hosted item passed to the mapper.

An explicit selection timestamp is normalized to UTC in `ExperimentSourceReference.Version`. A
latest selection leaves the version unset because Langfuse's public dataset metadata and item-list
responses do not expose a resolved latest-version identifier.

Required TDD:

- pagination and stable source ordering;
- version selection and source identity;
- input, expected-output, and metadata mapping;
- cancellation and malformed-response handling before execution;
- duplicate case-id validation;
- disabled mode does not silently pass.

### Phase 4B: Langfuse per-trial trace scope — #52

Prerequisites:

- #33 context-safe item execution;
- #34 dataset-run identity and link status;
- #35 idempotency and publication health;
- #49 provider lifecycle scopes.

Deliver:

- one scenario trace per statistical trial;
- every retry attempt beneath the same trial trace;
- one hosted dataset-run-item link per trial;
- trace-only local experiments when no hosted item is bound;
- namespaced trace, dataset-run-item, and dataset-run correlations;
- structured link/trace publication outcomes;
- strict and best-effort prerequisite behavior;
- one shared internal lifecycle implementation used by both the adapter and
  `ILangfuseExperimentRun.RunItemAsync`;
- coherent disabled mode.

The built-in client/session extension creates either a hosted provider bound to an existing
`ILangfuseExperimentRun` or a local trace-only provider. The hosted provider uses the case id as the
dataset item id, forwards the run's optional dataset-version timestamp, and leaves the same run
instance available to the Phase 4C result sink. Task and evaluator contexts receive the active
`ILangfuseScenario` as an exact-type feature.

Item publications use the `langfuse` namespace with `trace.id`, `dataset.run.item.id`, and
`dataset.run.id` correlations. Linked and local recorded traces complete their requested local
lifecycle work; failed or inconsistent links remain publication failures; not-sampled and disabled
scopes are not attempted. None of these states claims durable OpenTelemetry ingestion.

Required TDD:

- nested and parallel ambient-context restoration;
- delayed retry reactivation on another worker;
- no trace-per-attempt behavior;
- local trace-only mode;
- linked, failed, inconsistent, not-sampled, and disabled outcomes;
- strict failure stops execution while best effort preserves it;
- item evaluation can publish scores before scenario disposal;
- caller cancellation aborts and restores prior context.

### Phase 4C: Langfuse result sink and canonical guidance — #53

Deliver:

- item metric projection to trace scores;
- successful run-evaluation metric projection to dataset-run scores;
- explicit projection of the already-computed run decision without recalculating policy;
- generic publication status backed by the existing Langfuse publication snapshot;
- required and optional Langfuse publication behavior;
- the same experiment definition running locally, with disabled Langfuse, and with hosted Langfuse;
- the generic runner documented as the canonical bulk-experiment path;
- `ILangfuseExperimentRun` retained as the low-level/manual escape hatch.

The result sink uses the Langfuse item-scope publication to resolve each trace correlation, then
projects the existing mutable `EvaluationResult` rather than rebuilding metrics from normalized
snapshots. Successful run evaluators publish through the bound run's authoritative dataset-run
identity. Decision publication is explicit and categorical, using the already-reduced
`ExperimentRunDecision` without invoking policy again.

Provider details remain on `LangfuseExperimentResultSink.GetPublicationSnapshot()`: ordered item and
run-evaluation score outcomes, optional decision publication, and the hosted run's link/run-score
snapshot. Generic publication reduction observes only the sink's score work; item-link failures are
already represented by item-scope publication and are not counted again.

Required TDD:

- item and run score projection;
- decision projection without duplicate policy computation;
- unresolved and inconsistent dataset-run identity;
- one failing sink does not suppress another;
- disabled mode;
- publication status remains independent from quality decision;
- the same definition works with and without credentials.

Runnable example:

- one public experiment definition;
- local credential-free execution;
- disabled Langfuse execution with identical quality results;
- optional hosted source, trace links, and score publication when configured;
- no standalone telemetry ownership in a DI host.

### Phase 5: MEAI Reporting adapter as the second-provider proof — #54

Delivered in the dedicated `NexusLabs.Needlr.AgentFramework.Evaluation.Reporting` package.

Deliver:

- one `ScenarioRun` per trial;
- execution/scenario/iteration mapping;
- explicit case/trial replay, fresh-per-run, and disabled response-reuse modes;
- coordinated item scope and single item-evaluator integration;
- MEAI result-store persistence as item publication outcome;
- report generation left to MEAI tooling;
- Reporting-specific dependencies isolated from the provider-neutral core.

Required TDD:

- real `ReportingConfiguration` and test store/cache implementations;
- `CompositeEvaluator` behavior preserved;
- one evaluation per successful item;
- no execution retry on evaluator or store failure;
- cache isolation across trial and run modes;
- response-cache replay across retries documented and verified;
- disposal persists completed results;
- result-store failure appears as item publication failure;
- cancellation abort does not persist an incomplete `ScenarioRun`;
- caller cancellation preserved.

Runnable example:

- credential-free deterministic `IChatClient`;
- cached deterministic replay and fresh stochastic mode;
- generated MEAI report from the configured store.

## Rejected alternatives

- Use Langfuse item and result types as Needlr's canonical model.
- Treat a retry as another statistical sample.
- Drop failed items before run evaluation.
- Make telemetry or result-store availability redefine model quality.
- Reimplement MEAI `CompositeEvaluator`, response caches, stores, or reports.
- Reuse `AgentScenarioRunner` or `PipelineScenarioRunner` as the collection contract.
- Put xUnit fixtures, theory data, collection attributes, or assembly-global gates in core.
- Create a process-global semaphore or static coordination registry.
- Copy Langfuse's concurrency default or its failed-item omission.
- Create one task for every source item and rely only on a semaphore.
- Add hidden random retry jitter.
- Automatically retry non-idempotent sinks.
- Return a success-shaped partial result after caller cancellation.
- Ship a universal statistical policy for binary, continuous, paired, and clustered data.

## Deferred concerns

- streaming or unbounded case sources;
- checkpoint/resume;
- distributed workers;
- adaptive sampling and early stopping;
- cross-run baseline storage;
- paired and clustered statistical helpers;
- durable partial artifacts after cancellation;
- a broad built-in statistical policy catalog.

These concerns may be added after the finite runner and its result contract are exercised by runnable examples.

## Consequences

### Positive

- Needlr consumers receive one reusable orchestration model instead of rebuilding schedulers.
- Deterministic and stochastic evaluations share the same execution semantics.
- Failures remain visible and statistically honest.
- MEAI and Langfuse remain replaceable adapters.
- CI quality and publication health are independently actionable.
- The design remains independent of test frameworks and agent-loop implementations.

### Negative

- Needlr assumes long-term ownership of scheduler and result semantics.
- The public surface is larger than thin MEAI helpers.
- Item-scope composition adds async-context and lifecycle complexity.
- Statistical policy APIs require careful documentation to prevent misuse.

### Neutral

- This ADR does not immediately change existing single-scenario evaluators or quality gates.
- This decision does not add an implementation timeline.
- Langfuse-specific runner parity is not a goal.

## Outcome

Proceed with option A after this ADR is reviewed.

Do not proceed with a Langfuse-specific runner. Do not treat thin MEAI Reporting helpers as a complete solution.

## References

- [Needlr issue #36](https://github.com/ncosentino/needlr/issues/36)
- [Needlr evaluation documentation](../evaluation.md)
- [MEAI Evaluation libraries](https://learn.microsoft.com/en-us/dotnet/ai/evaluation/libraries)
- [MEAI evaluation reporting tutorial](https://learn.microsoft.com/en-us/dotnet/ai/evaluation/evaluate-with-reporting)
- [`ReportingConfiguration` 10.5.0 source](https://github.com/dotnet/extensions/blob/v10.5.0/src/Libraries/Microsoft.Extensions.AI.Evaluation.Reporting/CSharp/ReportingConfiguration.cs)
- [`ScenarioRun` 10.5.0 source](https://github.com/dotnet/extensions/blob/v10.5.0/src/Libraries/Microsoft.Extensions.AI.Evaluation.Reporting/CSharp/ScenarioRun.cs)
- [`CompositeEvaluator` 10.5.0 source](https://github.com/dotnet/extensions/blob/v10.5.0/src/Libraries/Microsoft.Extensions.AI.Evaluation/CompositeEvaluator.cs)
- [MEAI Reporting 10.8.0 on NuGet](https://www.nuget.org/packages/Microsoft.Extensions.AI.Evaluation.Reporting/10.8.0)
- [Langfuse experiments via SDK](https://langfuse.com/docs/evaluation/experiments/experiments-via-sdk)
- [Langfuse experiment data model](https://langfuse.com/docs/evaluation/experiments/data-model)
- [Langfuse experiments in CI/CD](https://langfuse.com/docs/evaluation/experiments/experiments-ci-cd)
- [Langfuse Python experiment runner source](https://github.com/langfuse/langfuse-python/blob/a02fc7c2195a81a6f75e084795f86857876b1c90/langfuse/_client/client.py)
- [Langfuse JavaScript experiment runner source](https://github.com/langfuse/langfuse-js/blob/a7ca64951da54448ff3397f21cf0ca378023be28/packages/client/src/experiment/ExperimentManager.ts)
- [Adding Error Bars to Evals](https://arxiv.org/abs/2411.00640)
- [A statistical approach to model evaluations](https://www.anthropic.com/research/statistical-approach-to-model-evals)
- [AI Agents That Matter](https://arxiv.org/abs/2407.01502)
