---
title: "ADR-0012: Bound mutation testing to runtime projects"
status: "Accepted"
date: "2026-08-16"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "testing", "mutation", "source-generation", "ci"]
supersedes: "ADR-0011"
superseded_by: ""
---

## Context and scope

ADR-0011 introduced advisory mutation testing with runtime and direct generator
implementation scopes. Hosted evidence showed that the runtime model works but the
generator model is not practical with the current Stryker.NET and xUnit v3 integration.

Stryker analyzes the complete project before applying the mutate filter. The narrowed
three-file generator configuration therefore still created 10,779 candidate mutants
across the 17,000-line generator project. After compile-error and mutate filtering, 330
mutants remained. Stryker's preview MTP runner discovered all 1,070 generator tests,
ignored the configured VSTest-style test-case filter, and reported zero covered mutants
after more than six minutes of coverage capture. The run then exceeded its
forty-five-minute limit.

By contrast, the runtime scope completed on the same runner in roughly two minutes.
A focused local MTP probe also killed 12 of 22 tested runtime mutants, confirming that
the xUnit v3 runner applies mutants correctly.

This decision supersedes ADR-0011's direct generator-project scope. It does not abandon
mutation testing for source-generation scenarios; it chooses a source-generated runtime
consumer as the bounded compatibility proof.

## Decision drivers

- Ready-pull-request feedback must remain bounded enough to be actionable.
- A mutate filter that still incurs whole-project mutant creation does not provide
  proportional isolation.
- xUnit v3 requires Stryker's preview MTP runner for meaningful mutant execution.
- Generated composition should remain exercised without claiming unsupported direct
  generator mutation coverage.
- Scope reduction must follow measured runtime and tool behavior rather than score
  optimization.
- Mutation scores remain advisory and artifact-free.

## Decision

Needlr retains two advisory mutation scopes:

- `runtime` mutates selected core dependency-injection and verification logic;
- `sourcegen` mutates the small Carter integration package while Carter tests compile
  and execute through Needlr's source-generated registry.

The direct `NexusLabs.Needlr.Generators` mutation configuration is removed. Generator
and generator-test changes select the `sourcegen` consumer scope so those changes still
prove that generated composition builds and behaves under mutation. Direct generator
implementation mutation remains deferred until Stryker can avoid whole-project mutant
construction and provide reliable MTP coverage/test filtering for this test project, or
Needlr introduces a separately justified smaller production boundary.

Both accepted scopes use MTP, Standard mutation level, per-test coverage analysis,
concurrency two, explicit authored source files, `thresholds.break = 0`, local
JSON/Markdown reporters, full-history checkout, and thirty-minute job limits. Neither
scope is a required branch check, uploads GitHub artifacts, or uses the Stryker
dashboard.

## Alternatives considered

### Allow the generator job ninety minutes

The first five-file scope exceeded seventy minutes and the three-file scope exceeded
forty-five minutes before completing. Raising the timeout would normalize poor feedback
latency without solving full-project mutant creation, failed coverage capture, or the
ignored test filter. It was rejected.

### Use mutation level Basic

Basic would reduce tested mutation kinds, but Stryker would still analyze the complete
generator project and MTP would still run against the full test set. It might reduce
runtime without correcting the dominant architecture problem. It was rejected as an
unmeasured score-shaping workaround.

### Build a mutation-only project that links production generator files

A small linked-source project could make Stryker see only selected files. It was
rejected for now because it creates a second compilation boundary whose references,
preprocessor symbols, analyzer options, and source layout can drift from the shipped
generator. Such a harness requires its own decision and parity checks.

### Remove source generation from mutation testing entirely

This would leave only the fast runtime scope. It was rejected because a small runtime
integration package can still prove that mutation testing coexists with generated
registries and module initialization without taking on direct generator-project cost.

## Consequences

### Positive

- Both mutation jobs have explicit thirty-minute ceilings.
- Source-generated composition remains in the mutation test path.
- The workflow no longer spends an hour on a generator run that cannot use its intended
  test or coverage filters.
- The scope reflects measured tool behavior rather than an arbitrary coverage target.

### Negative

- Needlr does not directly mutation-test generator implementation code.
- Generator changes receive compatibility evidence from a consumer rather than
  assertion-strength evidence inside generator algorithms.
- Future Stryker/xUnit improvements require reevaluation before direct generator
  mutation can return.

### Neutral

- Runtime mutation score remains advisory.
- Normal generator, analyzer, integration, package, and Native AOT tests remain required.
- No report persistence or external dashboard is introduced.

## Confirmation

`scripts/test-mutation.ps1` validates the runtime and source-generated consumer projects,
their explicit mutate lists, MTP selection, zero break thresholds, local reporters,
thirty-minute timeouts, full-history checkout, path classification, fork routing, and
non-required delivery declaration.

The corrective pull request must complete both scopes on PitCrew and expose readable job
summaries without artifact uploads. The source-generated consumer run must produce
non-empty mutant results within its timeout.

## References

- ADR-0011 records the initial direct-generator decision and is superseded by this
  measured correction.
- Stryker.NET documents MTP as preview with incomplete per-test filtering:
  <https://stryker-mutator.io/blog/stryker-net-mtp-runner/>.
- Stryker tracks xUnit v3 behavior under VSTest:
  <https://github.com/stryker-mutator/stryker-net/issues/3117>.
