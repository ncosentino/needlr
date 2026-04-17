---
# AUTO-GENERATED from .github/instructions/source-generators.instructions.md — do not edit
paths:
  - "**/*Generator.cs"
---
# Needlr Source Generator Rules

General source generator rules (class shape, structural discipline, netstandard2.0 constraints) are in `genesis/source-generators.instructions.md`. This file covers needlr-specific wiring only.

## Bootstrap wiring

- `NeedlrSourceGenBootstrap` (in `Generators.Attributes`) handles module-initializer registration for the main generator.
- `SourceGenRegistry` (in `NexusLabs.Needlr`) provides a decoupling layer between generated code and the core injection assembly.
- New features that produce config-bound registrations (like Options or HttpClient) piggyback on the existing `RegisterOptions` lambda — emit their code inside that method body so no new bootstrap plumbing is needed.

## Breadcrumbs

- Accept a `BreadcrumbWriter` instance from the orchestration method — never construct your own.
- Use `breadcrumbs.WriteFileHeader(builder, assemblyName, title)` for generated file headers.
- Use `breadcrumbs.WriteInlineComment(builder, indent, message)` for inline diagnostic comments.
