---
# AUTO-GENERATED from .github/instructions/genesis/roslyn-analyzers.instructions.md — do not edit
paths:
  - "**/*Analyzer.cs"
  - "**/*CodeFixProvider.cs"
  - "**/DiagnosticDescriptors.cs"
  - "**/AnalyzerReleases.*.md"
---
# Roslyn analyzers

- Choose one diagnostic-ID model per repository/package: product+component prefix or
  one short package prefix with a sequential numeric series. Never mix or reuse IDs.
- Use conventional categories; default to `Usage` when no more precise category fits.
- Severity is `Error` only for unambiguous defects, `Warning` for strong opt-in
  conventions, `Info` for suggestions, and `Hidden` only for IDE-only refactorings.
- Add every new diagnostic to `AnalyzerReleases.Unshipped.md` (`RS2000`).
- Message punctuation follows `RS1032`.
- Title/message state what is wrong and name the concrete fix visible in build output.
  Critical remediation cannot live only in `description`.
- Every descriptor has a stable rule-specific `helpLinkUri`.
- Compilation-end diagnostics include
  `WellKnownDiagnosticTags.CompilationEnd` (`RS1037`).
- Analyzer classes are public sealed, ignore generated code, enable concurrent
  execution, and forward compiler cancellation.
- Code-fix providers are public sealed, set an equivalence key, forward cancellation,
  and use batch fix-all unless ordering makes it unsafe.
- Do not add `SuppressMessage` merely to make the build green.
- Analyzer packages target `netstandard2.0` and set `IsRoslynComponent`,
  `EnforceExtendedAnalyzerRules`, `DevelopmentDependency`,
  `BuildOutputTargetFolder=analyzers/dotnet/cs`,
  `SuppressDependenciesWhenPacking`, and `NoPackageAnalysis`.
- Do not use the `IncludeBuildOutput=false`/manual `None Pack` shape that causes
  `NU5017`. A transient consumer smoke test proves the packed DLL loads.
