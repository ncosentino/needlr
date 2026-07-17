---
title: "ADR-0003: Provider-neutral experiment runner over MEAI Evaluation"
status: "Accepted"
date: "2026-07-12"
authors: "Nick Cosentino"
tags: ["architecture", "decision", "agent-framework", "evaluation", "experiments", "meai", "langfuse"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Needlr already supported individual MEAI evaluations, deterministic and LLM-judged
evaluators, response capture, quality thresholds, and Langfuse tracing and scoring.
It did not provide one reusable way to run a finite collection of cases with bounded
concurrency, repeated statistical trials, operational retries, failure isolation,
run-level evaluation, acceptance policy, and independent publication.

That gap affected at least two materially different workloads:

- deterministic regression suites, where each case usually runs once and every failure
  must remain visible; and
- stochastic evaluations, where one logical case has multiple independent trials and
  acceptance must report sample counts and uncertainty.

Microsoft.Extensions.AI.Evaluation.Reporting provides evaluator composition, response
caching, result storage, and report generation around a `ScenarioRun`. It is not a
collection scheduler or retry and policy engine. Langfuse's SDK experiment runners prove
that collection orchestration is useful, but their provider-specific data model and
failure semantics are not suitable as Needlr's canonical contract.

This decision governs finite in-process experiment orchestration and provider adapter
boundaries. It does not cover streaming or unbounded case sources, distributed workers,
checkpoint and resume, a universal statistical-method catalog, provider-specific
idempotency, or test-framework fixtures.

## Decision drivers

- Deterministic and stochastic workloads should share one execution and failure model.
- Trials must remain distinct from operational retry attempts.
- Bounded resource use, caller cancellation, and deterministic result ordering must be
  explicit.
- Failed, timed-out, canceled, and unevaluable items must remain visible to run-level
  analysis.
- Evaluation quality must remain independent from telemetry and persistence health.
- Dataset hosts, observability providers, and report stores must integrate without
  controlling scheduling or canonical results.
- Needlr should reuse MEAI's evaluator and metric abstractions rather than create a second
  evaluation ecosystem.
- The core API must remain independent of xUnit, NUnit, MSTest, and a particular agent
  loop.

## Decision

Needlr will own a provider-neutral experiment runner in
`NexusLabs.Needlr.AgentFramework.Evaluation.Experiments`.

The core runner owns:

- finite case materialization and validation;
- expansion of cases into statistically independent trials;
- bounded scheduling and deterministic result ordering;
- attempt history, timeout, cancellation, and explicit retry policy;
- whole-item failure isolation;
- one item-evaluation stage after terminal execution success;
- run-level evaluators and structured acceptance policies;
- per-trial lifecycle scopes for provider context;
- final result-sink fan-out;
- a canonical result and schema-versioned artifact model.

A bounded worker scheduler keeps pending work as data rather than creating one task per
item behind a semaphore. Delayed retries hold neither worker capacity nor shared admission
permits.

A trial is the statistical sample. A retry is another operational attempt for the same
trial and never increases the sample count. Run evaluators receive the complete ordered
item set, including failures, unless an explicit policy records an exclusion.

Caller cancellation stops admission, active work, retry delays, evaluation, policy, and
publication. It aborts entered incomplete scopes and propagates
`OperationCanceledException`; the runner does not return a partial success-shaped outcome
or continue into run evaluation, policies, or sinks.

`Microsoft.Extensions.AI.Evaluation.EvaluationResult` is the metric payload for item and
run evaluation. Needlr owns the surrounding experiment, failure, policy, publication, and
serialization contracts; it does not replace MEAI evaluator composition.

Provider integrations remain adapters:

- case sources may materialize local or hosted datasets before execution;
- item scopes may establish one provider lifecycle per trial and reactivate it around
  attempts and item evaluation;
- result sinks may publish the completed result;
- case-source loading and validation fail before execution and propagate to the caller;
- best-effort scope entry and activation failures are publication failures;
- execution-prerequisite scope failures prevent the next attempt and produce an explicit
  prerequisite item failure; and
- scope completion, disposal, and sink failures after quality is known remain publication
  failures.

Evaluation decision and publication status are separate. A model-quality result is not
changed by a failed trace export, dataset link, score upload, or result-store write.

Required policies reduce deterministically: any failed required policy makes the run
`Failed`; otherwise any inconclusive required policy makes it `Inconclusive`; otherwise
all required policies passing makes it `Passed`; and a run with no required policies is
`NotEvaluated`.

The initial built-in statistical policy evaluates a binary success proportion with
confidence bounds. Execution failure, timeout, and task-originated cancellation count as
failed samples. Evaluation failure, prerequisite failure, missing evidence, and invalid
metrics are explicit exclusions that make the result inconclusive unless the caller
selects pessimistic failure treatment. The policy reports item-status, success, failure,
sample, attempt, and exclusion counts so excluded or unknown evidence cannot disappear
from the reported accounting.

Langfuse-specific sources, trial scopes, and score sinks remain in
`NexusLabs.Needlr.AgentFramework.Langfuse`. MEAI Reporting-specific scenario caching and
persistence remain in `NexusLabs.Needlr.AgentFramework.Evaluation.Reporting`. Neither
provider's types become the canonical experiment model.

The runner requires explicit local concurrency and may accept a caller-owned shared
limiter. It will not create process-global mutable coordination or copy a provider's
default concurrency.

## Alternatives considered

### Leave orchestration to each consumer

This keeps Needlr's public API small and lets every workload choose its own scheduler.
It was rejected because consumers would continue to duplicate cancellation, retries,
failure accounting, result ordering, aggregate policy, and provider publication, producing
incompatible experiment semantics.

### Build only thin MEAI Reporting extensions

This has low implementation cost and follows Microsoft's public model closely. It was
rejected as the complete solution because `ReportingConfiguration` and `ScenarioRun` do
not schedule case collections, distinguish trials from retries, retain all failed items
for aggregation, or reduce run-level acceptance policy. MEAI Reporting remains an adapter
for the responsibilities it already owns.

### Build a Langfuse-specific runner

This would provide direct parity with Langfuse datasets and experiment-comparison views.
It was rejected because it would couple execution semantics to one provider, make local
and alternate-provider experiments second-class, and create a competing Needlr evaluation
model. Langfuse remains a hosted source, lifecycle, and publication adapter.

### Reuse `AgentScenarioRunner` or `PipelineScenarioRunner`

Those runners execute one Needlr-specific scenario shape and combine execution with
verification. They were rejected as the collection contract because experiments must be
independent of a particular agent loop, pipeline, or test framework.

## Consequences

### Positive

- Needlr consumers share one bounded and failure-honest experiment model.
- Deterministic suites and repeated stochastic trials use the same scheduling and result
  semantics.
- Retries cannot silently inflate statistical sample counts.
- Local data, Langfuse, MEAI Reporting, and future providers compose through independent
  boundaries.
- Quality policy and publication health remain separately actionable in CI and operations.
- Failed items remain available to aggregate evaluation instead of disappearing from the
  denominator.

### Negative

- Needlr assumes long-term ownership of scheduler, failure, result, and policy contracts.
- The public surface is larger than a thin wrapper over MEAI Reporting.
- Per-trial scope composition and cancellation-safe cleanup require careful asynchronous
  lifecycle testing.
- Statistical policy APIs can be misused if callers ignore exclusions, sample size, or
  uncertainty.
- Provider adapters may expose capability differences that the core cannot normalize
  without becoming provider-specific.

### Neutral

- Existing single-result evaluators and quality gates remain useful outside experiments.
- Provider SDKs retain ownership of their storage, scoring, tracing, and report-rendering
  semantics.
- Distributed execution and broader statistical methods require separate decisions.

## Confirmation

The repository confirms the decision through these stable boundaries:

- `ExperimentDefinition<TCase,TOutput>` composes a finite case source, task, one item
  evaluator, run evaluators, policies, per-trial scopes, and result sinks.
- `IExperimentRunner` returns a canonical quality result with independent publication
  outcomes.
- Experiment-runner tests cover bounded scheduling, retries, cancellation, failure
  isolation, run evaluation, policy, scope lifecycle, sink fan-out, and deterministic
  artifacts.
- `NexusLabs.Needlr.AgentFramework.Langfuse` implements hosted source, trial lifecycle,
  and result-sink adapters without owning the scheduler.
- `NexusLabs.Needlr.AgentFramework.Evaluation.Reporting` implements one MEAI
  `ScenarioRun` per trial without introducing a second scheduler or canonical result.
- Credential-free examples exercise the core runner and Reporting adapter; the Langfuse
  conformance example exercises disabled, local, and hosted provider shapes.

Repository tests cannot establish production provider availability, model quality, or the
statistical validity of a caller's chosen metric and sample design. Those require
workload-specific evidence.

## References

- [`docs/experiment-runner.md`](../experiment-runner.md) documents the implemented runner,
  lifecycle, retry, policy, and adapter semantics selected by this decision.
- [`docs/evaluation.md`](../evaluation.md) describes the MEAI evaluation primitives reused
  as the metric language rather than replaced by Needlr.
- [`docs/langfuse.md`](../langfuse.md) describes the provider-specific source, trace, and
  score adapters that remain outside the core runner.
- [MEAI Evaluation libraries](https://learn.microsoft.com/dotnet/ai/evaluation/libraries)
  define the evaluator, metric, caching, storage, and reporting responsibilities Needlr
  composes with.
- [Langfuse experiments via SDK](https://langfuse.com/docs/evaluation/experiments/experiments-via-sdk)
  provide the hosted experiment model that motivated adapter support without becoming the
  Needlr canonical contract.
- [Needlr issue #36](https://github.com/ncosentino/needlr/issues/36) contains supplemental
  design-spike and implementation history intentionally excluded from this permanent
  decision record.
