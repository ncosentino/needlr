---
title: "ADR-0011: Adopt bounded advisory mutation testing"
status: "Accepted"
date: "2026-08-15"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "testing", "mutation", "source-generation", "ci"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Needlr's unit and integration tests verify expected behavior, but line coverage cannot
show whether an assertion would detect a plausible defect. Mutation testing provides
that additional signal by changing production code and checking whether tests fail.

Needlr also contains Roslyn source generators and analyzers. Stryker.NET 4.16.0 mutates
authored project syntax trees and reruns source generators while compiling mutants;
generated syntax trees are not direct mutation inputs. Tests that invoke generators
through Roslyn at runtime can therefore kill generator-implementation mutants, while a
consumer test assembly built once before mutation does not provide equivalent evidence.
Analyzer execution remains a separate concern because normal mutant compilation does not
replace analyzer-specific test harnesses.

Mutation runs can be expensive, and the repository has no reviewed mutation baseline.
The initial integration must not invent a threshold, become a required merge check, add
GitHub artifact storage, or upload source-level reports to an external dashboard.

This decision governs the initial Stryker.NET scopes, CI cadence, reporting, and score
policy. It does not establish a permanent mutation threshold or claim repository-wide
mutation coverage.

## Decision drivers

- Mutation testing must expose weak assertions that line coverage cannot distinguish.
- Source-generator implementation code needs direct Roslyn test-harness evidence.
- Initial runtime and cost must remain bounded and measurable.
- Mutation score must not block delivery before survivors and uncovered mutants are
  reviewed.
- Tool, build, and initial-test failures must remain visible rather than being converted
  into successful mutation reports.
- Reports must remain available in the workflow without creating more GitHub artifacts
  or sending repository data to a third-party dashboard.
- External fork pull requests must stay off self-hosted infrastructure.

## Decision

Needlr pins `dotnet-stryker` 4.16.0 in the repository tool manifest and defines two
explicit configurations.

The `runtime` scope mutates selected dependency-injection, verification, and diagnostic
logic in `NexusLabs.Needlr`, using `NexusLabs.Needlr.Tests`.

The `generators` scope mutates selected authored identifier, constant-rendering, and
breadcrumb helper files in
`NexusLabs.Needlr.Generators`, using focused tests in
`NexusLabs.Needlr.Generators.Tests` that invoke Roslyn generators at runtime. It does
not attempt to mutate generated syntax trees. Analyzer projects and the full generator
project remain outside the initial scope until their cost and signal are measured
separately.

An initial five-file candidate also included bootstrap emitters. Its first hosted run
exceeded seventy minutes and was cancelled, while the runtime scope completed in under
three minutes. The accepted generator scope therefore retains only the three directly
tested deterministic helpers and has a forty-five-minute job timeout. Scope is reduced
because of measured feedback latency, not to improve its score.

Both scopes use Stryker's Microsoft Testing Platform runner, Standard mutation level,
per-test coverage analysis, concurrency two, and explicit file lists. Needlr uses
xUnit v3; a VSTest probe discovered tests but reported every tested mutant as surviving,
which is consistent with Stryker's tracked xUnit v3 limitation. MTP is preview, so
suspicious outcomes must be compared with coverage analysis disabled before they become
policy. Each scope uses `thresholds.break = 0`.
A low score is therefore advisory; Stryker setup failures, compilation failures, and
initial test failures still return failure.

The mutation workflow:

- runs affected scopes for ready pull requests;
- runs both scopes weekly and on manual dispatch;
- is declared non-required in the delivery contract;
- routes trusted runs through Needlr's configured CI runner and external forks through
  GitHub-hosted runners;
- checks out full history because the selected projects use NBGV and SourceLink during
  Stryker's initial build;
- writes Markdown results to the GitHub job summary and leaves JSON/Markdown reports
  ephemeral on the runner;
- does not upload artifacts and does not enable the Stryker dashboard.

Regular CI validates the tool pin, bounded configs, score policy, workflow triggers,
fork routing, artifact prohibition, and change classifier.

## Alternatives considered

### Mutate the complete solution immediately

A complete run would maximize breadth and avoid selecting initial scopes. It was
rejected because Stryker mutates one project at a time, Needlr has many projects, and
the runtime and survivor volume would be unknown. A broad aggregate score would also
hide which subsystem needs stronger tests.

### Exclude source-generator implementation code

This avoids Roslyn-specific uncertainty. It was rejected because Stryker reruns source
generators during mutant compilation, and Needlr already has tests that invoke generator
drivers directly. A bounded authored-generator scope provides useful evidence without
claiming that generated output itself is mutated.

### Use the VSTest runner

VSTest is Stryker's stable default and Needlr carries the xUnit VSTest adapter. It was
rejected after a direct probe discovered and ran the xUnit v3 tests but killed none of
the selected mutants. Stryker tracks xUnit v3 handling as an incompatibility and added
MTP specifically for modern test frameworks. The preview MTP runner is therefore the
only useful initial path, with its limitations kept outside required policy.

### Add a mutation threshold immediately

Any positive break threshold would create a gate before the repository has classified
survivors, uncovered mutants, compile errors, and equivalents. It was rejected. A later
decision may ratchet one measured scope after triage.

### Persist reports or baselines

GitHub artifacts, dashboard uploads, or disk-baseline caches would improve report
retention or incremental execution. They were rejected because the initial requirement
forbids additional artifact storage, dashboard upload would cross a new external data
boundary, and disk baselines have no value on ephemeral runners without persistence.

## Consequences

### Positive

- Needlr gains direct evidence about assertion strength in runtime and generator code.
- Source generation is exercised through the same Roslyn runtime tests that own its
  behavior.
- Pull-request cost is limited by path classification and bounded source lists.
- Mutation score is visible without prematurely blocking delivery.
- No new GitHub artifact storage or external reporting service is required.

### Negative

- Initial mutation coverage is intentionally incomplete.
- VSTest may be slower than the preview MTP runner.
- Reports disappear with the runner after their summary has been written.
- Surviving mutants require human triage before any threshold can be justified.

### Neutral

- Mutation testing remains separate from the required `CI` summary.
- Normal unit, integration, package, and Native AOT checks remain the correctness and
  delivery gates.
- A future expansion can add scopes or thresholds without changing the source-generation
  model established here.

## Confirmation

`scripts/test-mutation.ps1` validates the pinned tool, explicit scopes, MTP selection,
Standard mutation level, zero break thresholds, local reporters, workflow cadence,
non-required delivery declaration, fork routing, and prohibition on artifact/dashboard
publication.

The first ready pull-request run must demonstrate that both Stryker scopes complete on
the repository runner, emit non-empty reports, append readable GitHub job summaries, and
do not upload artifacts. Its measured duration and mutant outcomes become the evidence
for survivor triage and any future scope-specific threshold.

## References

- Stryker.NET configuration documents project selection, mutate globs, VSTest/MTP
  runners, coverage analysis, concurrency, reporters, and threshold behavior:
  <https://stryker-mutator.io/docs/stryker-net/configuration/>.
- Stryker.NET reporter documentation defines local JSON and Markdown reporters and the
  separate dashboard upload boundary:
  <https://stryker-mutator.io/docs/stryker-net/reporters/>.
- Stryker.NET 4.16.0 is the pinned mutation tool:
  <https://github.com/stryker-mutator/stryker-net/releases/tag/dotnet-stryker%404.16.0>.
- Stryker tracks xUnit v3 behavior under the VSTest runner:
  <https://github.com/stryker-mutator/stryker-net/issues/3117>.
