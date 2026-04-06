# Needlr — AI Agent Instructions

Needlr is an opinionated dependency injection framework for .NET with compile-time source generation as the primary discovery strategy. Reflection is opt-in.

## Build & Test

```bash
dotnet build src/NexusLabs.Needlr.slnx
dotnet test src/NexusLabs.Needlr.slnx
```

## File Conventions

- **One type per file.** Never put multiple classes, structs, or enums in the same `.cs` file.
- **Never save files to the repo root.** Source code lives under `src/`, docs under `docs/`, scripts under `scripts/`.
- **File-scoped namespaces** everywhere (`namespace Foo;`, not `namespace Foo { }`).
- **`internal` by default** for types that are not part of the public API surface. Only types consumers reference directly should be `public`.

## Naming

- PascalCase for all public members, types, and namespaces.
- `_camelCase` for private fields.
- Diagnostic IDs use the `NDLR` prefix with a component code: `NDLRCOR` (core), `NDLRGEN` (generators), `NDLRMAF` (agent framework), `NDLRSIG` (SignalR), `NDLRHTTP` (HttpClient).

## XML Documentation

Required on all `public` types and members. Use `<summary>`, `<param>`, `<returns>`, and `<example>` tags where appropriate. Internal types should have XML docs on the class itself; individual members are optional unless non-obvious.

## Central Package Management

All NuGet package versions are declared in `src/Directory.Packages.props` (`ManagePackageVersionsCentrally=true`). Individual `.csproj` files reference packages by name only — never specify a version inline.

## Architecture

The codebase follows a consistent per-feature pattern:

| Layer | Location | Role |
|-------|----------|------|
| **Attribute** | `NexusLabs.Needlr.Generators.Attributes/` | User-facing marker (`[Options]`, `[HttpClientOptions]`, `[GenerateFactory]`, etc.). Targets `netstandard2.0`. |
| **Discovery helper** | `NexusLabs.Needlr.Generators/*DiscoveryHelper.cs` or `*AttributeHelper.cs` | Roslyn-side logic that reads the attribute from `INamedTypeSymbol` and extracts a model struct. |
| **Model** | `NexusLabs.Needlr.Generators/Models/` | `internal readonly struct` holding discovered metadata. One type per file, organized into feature subfolders. |
| **Code generator** | `NexusLabs.Needlr.Generators/CodeGen/*CodeGenerator.cs` | `internal static class` that emits C# source text into a `StringBuilder`. |
| **Analyzer** | `NexusLabs.Needlr.Generators/*Analyzer.cs` | `DiagnosticAnalyzer` enforcing compile-time contracts for the feature's attribute. |
| **Integration tests** | `NexusLabs.Needlr.IntegrationTests/SourceGen/` | xUnit tests that build a real `Syringe` service provider and verify the generated code runs correctly. |
| **Docs** | `docs/<feature>.md` + `docs/analyzers/NDLRXXX.md` | Feature page + per-diagnostic reference page, registered in `mkdocs.yml` nav. |

When adding a new source-generated feature, follow ALL layers of this pattern — don't skip the analyzer, the docs, or the integration tests.

## Glob-Targeted Instructions

Pattern-specific rules live in `.github/instructions/*.instructions.md`. These activate automatically when you edit files matching their glob. See that directory for rules covering source generators, analyzers, CodeGen emission, attributes, integration tests, discovery helpers, models, docs, examples, and project files.
