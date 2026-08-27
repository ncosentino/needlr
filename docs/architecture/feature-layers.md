# Per-feature layer pattern

Needlr's source-generated features follow one consistent shape. A feature that skips a
layer is incomplete, even when it works.

| Layer | Location | Role |
|-------|----------|------|
| **Attribute** | `NexusLabs.Needlr.Generators.Attributes/` | User-facing marker (`[Options]`, `[HttpClientOptions]`, `[GenerateFactory]`, and similar). Targets `netstandard2.0`. |
| **Discovery helper** | `NexusLabs.Needlr.Generators/*DiscoveryHelper.cs` or `*AttributeHelper.cs` | Roslyn-side logic that reads the attribute from an `INamedTypeSymbol` and extracts a model struct. |
| **Model** | `NexusLabs.Needlr.Generators/Models/` | `internal readonly struct` holding discovered metadata. One type per file, organized into feature subfolders. |
| **Code generator** | `NexusLabs.Needlr.Generators/CodeGen/*CodeGenerator.cs` | `internal static class` that emits C# source text into a `StringBuilder`. |
| **Analyzer** | `NexusLabs.Needlr.Generators/*Analyzer.cs` | `DiagnosticAnalyzer` enforcing the feature's compile-time contracts. |
| **Integration tests** | `NexusLabs.Needlr.IntegrationTests/SourceGen/` | xUnit tests that build a real `Syringe` service provider and verify the generated code runs. |
| **Docs** | `docs/<feature>.md` plus `docs/analyzers/NDLRXXX.md` | Feature page and per-diagnostic reference page, both registered in `mkdocs.yml` nav. |

When adding a new source-generated feature, follow **all** layers of this pattern. Do not
skip the analyzer, the docs, or the integration tests.

## Why every layer matters

The analyzer is the layer most often skipped, and it is the one that turns a silent
misuse into a compile error. A generator that quietly emits nothing when an attribute is
applied incorrectly produces a runtime failure far from its cause.

The integration tests are the second most often skipped. Generator unit tests assert on
emitted text; only an integration test proves the emitted code compiles, registers, and
resolves through a real container.

## See also

- [Deterministic generator output](../development/deterministic-generators.md)
- [Roslyn analyzers](../development/roslyn-analyzers.md)
- [.NET engineering conventions](../development/dotnet-engineering.md)
