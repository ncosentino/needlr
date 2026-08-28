---
# AUTO-GENERATED from .github/instructions/generator-determinism.instructions.md — do not edit
paths:
  - "**/NexusLabs.Needlr.Generators/*.cs"
  - "**/NexusLabs.Needlr.Generators/CodeGen/**/*.cs"
  - "**/NexusLabs.Needlr.Generators/Export/**/*.cs"
  - "**/NexusLabs.Needlr.Roslyn.Shared/**/*.cs"
---
# Deterministic Generated Output

Everything that can reach `AddSource` must be a pure function of compilation and
analyzer-config inputs.

- Do not read wall-clock time, randomness, process state, user identity, or machine
  identity.
- Format emitted integers through `GeneratorHelpers.Literal`; use invariant casing.
- Sort emitted string populations with an explicit ordinal comparer.
- Normalize source locations through `BreadcrumbWriter.GetRelativeSourcePath`; never
  emit absolute paths.
- Create source text through `GeneratedSourceText.Create`. Direct `SourceText.From` is
  banned.
- Keep timestamps out of compiled source. Stamp human-readable artifacts at their
  write boundary, using a schema-valid placeholder when the embedded value must parse.
- Fix `RS0030` violations. The single suppression inside `GeneratedSourceText` is the
  only sanctioned bypass.

See `docs/development/deterministic-generators.md` for rationale and failure cases.
