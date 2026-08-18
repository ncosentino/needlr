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

Needlr defines fourteen advisory runtime-package scopes in one committed manifest.
Every scope declares its project, direct test project, source/test trigger roots,
numeric priority, and at most five representative files.

The direct `NexusLabs.Needlr.Generators` mutation configuration is removed. Generator
and generator-test changes select the `sourcegen` consumer scope so those changes still
prove that generated composition builds and behaves under mutation. Direct generator
implementation mutation remains deferred until Stryker can avoid whole-project mutant
construction and provide reliable MTP coverage/test filtering for this test project, or
Needlr introduces a separately justified smaller production boundary.

Mutation testing is pull-request-only. The classifier selects affected scopes and uses
Stryker changed-code analysis against `origin/main`. It selects no more than two scopes
per pull request and no more than five changed source files per scope. Scope selection
uses committed numeric priority; file selection uses changed-line count followed by
path. Every omitted scope and file is reported.

Selected jobs run with maximum parallelism two and a ten-minute timeout. Generated
configs use MTP, Standard mutation level, per-test coverage analysis, concurrency two,
`thresholds.break = 0`, and local JSON/Markdown reporters. The workflow is not required,
does not persist baselines or reports, does not run on a schedule, does not upload
artifacts, and does not use the Stryker dashboard.

The lightweight selector reads changed-file metadata through GitHub's pull-request API
instead of fetching full history. Only selected Stryker jobs fetch full history for NBGV,
SourceLink, and `since: origin/main`.

Each internal scope run upserts one neutral GitHub Check with duration, counts, mutated
files, actionable survivors/uncovered mutants, and a workflow link. After all selected
scopes complete, the workflow upserts one concise PR comment containing only the scope
table and links to those detailed Checks. This is delivery evidence, not report
persistence: reruns replace the current-head comment, raw JSON/Markdown stays on the
runner, and no cross-run baseline is retained. Forks keep the job summary but cannot
publish comments or checks with their read-only token.

Each scope's test project directly references the project under mutation. Stryker uses
that direct edge to select the mutation target; a merely transitive reference can produce
a fast but meaningless run whose tests never observe the mutant assembly.

## Alternatives considered

### Run complete project scopes on every pull request

This maximizes each run's breadth. It was rejected because mutation testing would add
unbounded feedback time and repeat work unrelated to the pull request. Changed-code
selection provides direct feedback on the code currently being reviewed.

### Add scheduled complete runs

Scheduled rotation would eventually inspect untouched code and support trend analysis.
It was rejected for the current integration because the repository does not need
historical reports or baseline persistence, and the immediate goal is bounded PR
feedback.

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

- Every mutation job has an explicit ten-minute ceiling.
- Fourteen directly tested runtime packages are eligible when their code or tests change.
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

`scripts/test-mutation.ps1` validates all scope mappings and priority files, the two-scope
and five-file limits, deterministic trimming, MTP selection, zero break thresholds,
ephemeral local reporters, ten-minute timeout, full-history checkout, changed-code
selection, fork routing, and non-required delivery declaration.

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
