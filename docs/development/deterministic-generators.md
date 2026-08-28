# Deterministic generator output

Needlr sets `<Deterministic>true</Deterministic>` in `src/Directory.Build.props`. That
flag only guarantees the *compiler* is a pure function of its inputs — it cannot detect a
source generator that reads ambient state. Anything passed to `AddSource` is compiled
into the consumer's assembly, so a value read from the clock makes the assembly hash
change between builds of identical source.

## The rule

Code that runs inside a generator must not read ambient state. Concretely, no
`AddSource` output may depend on:

- the wall clock (`DateTime.UtcNow`, `DateTimeOffset.Now`, and friends);
- randomness (`Guid.NewGuid`, `Random`);
- host identity (`Environment.MachineName`, `Environment.UserName`);
- unordered enumeration — sort discovered symbols before emitting them.

## Enforcement

`src/NexusLabs.Needlr.Generators/BannedSymbols.txt` bans these APIs through
`Microsoft.CodeAnalysis.BannedApiAnalyzers`. Because `TreatWarningsAsErrors` is enabled,
a clock read fails the build with `RS0030` rather than silently shipping.

`GeneratedSourceDeterminismTests` asserts that no generated file contains a
timestamp-shaped value, which also covers routes the banned-API list cannot see (for
example, formatting a `DateTime` constructed by hand).

Two seemingly general enforcement approaches do not work for generator culture:

- `CA1305` does not report interpolated numeric values in this project.
- Roslyn rule `RS1035` prohibits analyzers and generators from mutating
  `CultureInfo.CurrentCulture`.

The culture-invariance test therefore runs generation under multiple hostile locales
and compares every generated file.

!!! note "Why not just run the generator twice and compare?"

    A comparative test does not detect a clock read. Timestamps rendered at one-second
    resolution are identical across two in-process runs, so such a test passes while the
    defect is present and fails only when a run happens to straddle a second boundary.
    Assert the invariant structurally instead.

## Determinism is per-machine, per-OS, and per-locale

A rebuild that is byte-identical on one machine is not sufficient. Generated output must
also not depend on the host's operating system or the build machine's locale:

- **Numbers** go through `GeneratorHelpers.Literal`. Locales including `sv-SE`, `fi-FI`,
  and `lt-LT` format a negative number with U+2212 MINUS SIGN, which is not valid C#.
- **String sorts** take an explicit `StringComparer.Ordinal`. The default comparer is
  culture-sensitive, so emission order changes with the machine's locale.
- **Source paths** go through `BreadcrumbWriter.GetRelativeSourcePath`, which normalizes
  separators to `/` and never emits an absolute path.
- **Line endings** are normalized to LF at the `AddSource` boundary.

## Keeping a timestamp you actually want

A timestamp is legitimate in a human-readable report. Stamp it where the artifact is
**written**, never where the source is generated:

| Artifact | Emitted into source as | Real time stamped by |
|---|---|---|
| Diagnostic markdown reports | `DiagnosticsGenerator.GeneratedAtPlaceholder` | the `NeedlrExtractDiagnostics` MSBuild task in `NexusLabs.Needlr.Generators.targets` |
| IDE graph JSON | `GraphExporter.GeneratedAtSentinel` | the generated `NeedlrGraphExport.WriteGraphToFile` method |

The sentinel used for the graph is a schema-valid RFC 3339 value, so the embedded JSON
still satisfies `schemas/needlr-graph-v1.schema.json` while remaining constant.

## Verifying

Build any project that emits a service catalog twice and compare hashes:

```powershell
dotnet build src\Examples\SourceGen\CarterSourceGen\CarterSourceGen.csproj -c Release -t:Rebuild
Get-FileHash src\Examples\SourceGen\CarterSourceGen\bin\Release\net10.0\CarterSourceGen.dll
```

Two runs must produce the same SHA-256.

## Line endings

`StringBuilder.AppendLine` uses `Environment.NewLine`, so emitters naturally produce CRLF
on Windows and LF elsewhere. Emitted text is normalized to LF once at the `AddSource`
boundary by `GeneratedSourceText.Create`, which every generator routes through.

`SourceText.From` is banned in `BannedSymbols.txt` so a new generator cannot bypass the
helper; the single sanctioned call inside the helper carries a documented
`#pragma warning disable RS0030`.

Fixing this at the emitters was not viable: there are roughly 1,272 `AppendLine` call
sites against 16 `AddSource` boundaries.

Generator tests that compare multiline output normalize expected raw-string literals
with `.ReplaceLineEndings("\n")`; otherwise the expectation inherits the checkout's
line endings and agrees with broken platform-dependent output by coincidence.

## Analyzer-config test doubles

Roslyn compares analyzer-config keys case-insensitively through
`AnalyzerConfigOptions.KeyComparer`. Test doubles use the same comparer. A default
`Dictionary<string, string>` is case-sensitive and can make a test silently exercise
the generator's "option absent" branch rather than the behavior it claims to verify.
