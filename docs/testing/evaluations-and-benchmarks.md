# Testing and benchmarks

Needlr uses deterministic tests to protect behavior and BenchmarkDotNet measurements
to evaluate performance. Tests remain the correctness authority; benchmarks compare
equivalent production paths after correctness is established.

## Mutation testing

Stryker.NET measures whether focused tests detect meaningful changes to authored
production code. It supplements normal tests; it does not replace them or prove that
behavior is correct.

Needlr maps fourteen directly tested runtime packages in
`scripts/mutation/scopes.json`. Each scope declares its production project, direct test
project, deterministic priority, and at most five representative files for test-only or
tooling changes.

Roslyn-generated syntax trees are not direct mutation targets. Stryker mutates the
generator's authored input syntax trees, reruns the source generators while compiling
the mutant, and then executes tests. The generator scope therefore uses tests that
invoke Roslyn generators at runtime; consumer projects that only compiled generated
output before the mutation run would not provide equivalent evidence.

Needlr uses xUnit v3, so Stryker runs through its Microsoft Testing Platform runner.
The VSTest runner can discover the tests but does not reliably apply mutants to xUnit
v3 execution. MTP remains preview in Stryker.NET 4.16.0; suspicious mutation outcomes
must be compared with coverage analysis disabled before they become policy.

Direct mutation of the generator implementation is deferred. Stryker analyzes the full
generator project before applying its mutate filter, creating more than ten thousand
candidate mutants for even a three-file scope. MTP then failed coverage capture for the
generator test project and left hundreds of mutants to run against the full test suite.
The source-generated consumer scope keeps generated composition in the test path without
pretending that this toolchain currently provides practical generator-implementation
mutation testing.

Run one scope locally:

```powershell
dotnet tool restore
pwsh scripts/run-mutation-tests.ps1 `
  -Scope core `
  -MutateFiles ServiceProviderExtensions.cs `
  -SinceTarget origin/main
```

Reports are written under `artifacts/mutation/`, which is ignored by git.

The mutation workflow is pull-request-only, advisory, and not a required check. It:

- uses Stryker changed-code analysis against `origin/main`;
- selects at most two scopes by committed numeric priority;
- selects at most five changed source files per scope, ordered by changed-line count
  then path;
- runs at most two jobs concurrently;
- caps every job at ten minutes;
- reports every omitted scope and file explicitly.

Each generated configuration uses `thresholds.break = 0`, so a low mutation score is
reported but does not fail the workflow. Tool failures, build failures, and initial test
failures still fail loudly.

Mutation JSON and Markdown reports are ephemeral runner files. The workflow copies the
summary plus every surviving or uncovered mutant into the GitHub job summary. Internal
pull requests receive one neutral Check per selected scope containing the same details,
plus one concise upserted PR comment with the scope table and links to those Checks. The
comment is intentionally summary-only so detailed mutants do not overwhelm the PR
conversation. These PR surfaces replace their prior current-head result; they are not
baselines or report archives. The workflow does not upload GitHub artifacts, cache
baselines, schedule full runs, or send results to the Stryker dashboard.

## Benchmark harnesses

Benchmark methods contain only the production call under test. Setup, data generation,
validation, logging, and assertions live outside timed code.

Comparisons run baseline and candidate in the same class/run with the same input and
consumption pattern. A strategy switch inside one benchmark measures the branch as
well as the strategy and is not a valid comparison.

Benchmarks call real production code through a project reference. Correctness tests in
the normal test project prove equivalent outcomes.

Use memory diagnostics and representative parameter ranges. The scheduled workflow
publishes machine/runtime metadata with results; compare measurements only when the
environment and benchmark definition are equivalent.

The public [Benchmarks](../benchmarks.md) page is the canonical published result.
