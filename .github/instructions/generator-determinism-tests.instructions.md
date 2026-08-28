---
applyTo: "**/NexusLabs.Needlr.Generators.Tests/GeneratedSource*Tests.cs,**/NexusLabs.Needlr.Generators.Tests/GeneratorTestRunner.cs,**/NexusLabs.Needlr.Generators.Tests/Diagnostics/DiagnosticTestHelpers.cs"
---

# Generator Determinism Tests

- Exercise hostile culture, Windows-style paths, and LF line endings explicitly; do
  not rely on the test host.
- Demonstrate the failing state before accepting a new determinism guard.
- Compare complete generated files for cross-cutting invariants and retain a focused
  assertion that names the concrete defect.
- Normalize multiline expected source with `.ReplaceLineEndings("\n")`.
- Analyzer-config test doubles use `AnalyzerConfigOptions.KeyComparer`.
