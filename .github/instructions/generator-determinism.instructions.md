---
applyTo: "**/NexusLabs.Needlr.Generators/**,**/NexusLabs.Needlr.Generators.Tests/**/*.cs,**/NexusLabs.Needlr.Roslyn.Shared/**/*.cs"
---

# Generated Output Determinism

Needlr sets `<Deterministic>true</Deterministic>` in `src/Directory.Build.props`. That
flag only guarantees the compiler is a pure function of its inputs — it cannot detect a
generator that reads ambient state. Everything passed to `AddSource` is compiled into the
consumer's assembly, so ambient state there changes the assembly hash between builds of
identical source and breaks content-addressed build caches.

## Never read ambient state inside a generator

No `AddSource` output may depend on:

- the wall clock — `DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow`, `DateTimeOffset.Now`, `DateTime.Today`;
- randomness — `Guid.NewGuid`, `Random`;
- host identity — `Environment.MachineName`, `Environment.UserName`, `Environment.TickCount`;
- unordered enumeration — sort symbols with an explicit `StringComparer` before emitting.

`BannedSymbols.txt` enforces the API list through `Microsoft.CodeAnalysis.BannedApiAnalyzers`.
`TreatWarningsAsErrors` is on, so a violation fails the build with `RS0030`. Do not
suppress it — fix the emitter.

## Stamp timestamps where the artifact is written

A timestamp is legitimate in a human-readable report, but it must never reach `AddSource`.
Emit a constant placeholder and substitute the real value at the point the artifact is
written to disk. Both existing cases follow this shape:

| Artifact | Placeholder emitted into source | Substituted by |
|---|---|---|
| Diagnostic markdown | `DiagnosticsGenerator.GeneratedAtPlaceholder` | `NeedlrExtractDiagnostics` task in `build/NexusLabs.Needlr.Generators.targets` |
| IDE graph JSON | `GraphExporter.GeneratedAtSentinel` | generated `NeedlrGraphExport.WriteGraphToFile` |

When a placeholder must survive schema validation, make it a valid value of the declared
type — the graph sentinel is a schema-valid RFC 3339 timestamp so the embedded JSON still
conforms to `schemas/needlr-graph-v1.schema.json`.

## Testing determinism

Assert the invariant **structurally**. Do not write a test that runs the generator twice
and diffs the output: timestamps render at one-second resolution, so two in-process runs
produce identical text and the test passes while the defect is present, failing only when
a run straddles a second boundary. That is a flaky test that proves nothing.

Add new emitters to `GeneratedSourceDeterminismTests`, which asserts that no generated
file contains a timestamp-shaped value once known placeholders are removed.

## Reference

Full rationale, reproduction steps, and the cross-OS newline limitation are in
`docs/development/deterministic-generators.md`.
