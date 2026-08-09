# Roslyn analyzers

Analyzer diagnostics are APIs consumed by IDEs, CI logs, humans, and coding agents.
Their identifiers and visible messages need the same stability and clarity as other
public contracts.

## Identifier strategy

Choose one repository-wide diagnostic-ID model:

- product prefix plus component code; or
- one short package prefix plus a sequential numeric series.

Do not mix models inside a package, reuse retired IDs, or collide with established
compiler/analyzer prefixes.

## Descriptor design

Use conventional categories so consumers can configure them consistently. Default
severity reflects confidence and installation context: errors are unambiguous defects;
warnings are strong opt-in conventions; informational diagnostics are advisory.

The build-visible title and message explain both the defect and the concrete fix.
Descriptions add rationale but cannot hide required remediation that CI/agents never
see.

Every diagnostic has a stable rule-specific help link with motivation, good/bad
examples, suppression guidance, and related rules.

## Implementation shape

Analyzers are public sealed classes, ignore generated code, enable concurrent
execution, and propagate compiler cancellation.

Compilation-end diagnostics carry `WellKnownDiagnosticTags.CompilationEnd`.

Code fixes are public sealed providers with stable equivalence keys, cancellation
propagation, and fix-all support unless ordering makes batch application unsafe.

## Release and packaging

New diagnostics enter `AnalyzerReleases.Unshipped.md` in the same change; RS2000
enforces the release record. Message punctuation follows RS1032.

Analyzer packages target the repository's canonical Roslyn packaging shape and verify
the produced package by installing it into a transient consumer. A package that builds
but does not load its analyzer DLL is not a valid release.

Suppressing an analyzer to make the build green hides the contract. Fix the code unless
an explicit repository decision approves a suppression.
