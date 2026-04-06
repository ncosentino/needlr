---
# AUTO-GENERATED from .github/instructions/source-generators.instructions.md — do not edit
paths:
  - "**/*Generator.cs"
---
# Source Generator Rules

## Class shape

- Decorate with `[Generator(LanguageNames.CSharp)]`, implement `IIncrementalGenerator`.
- Generator projects target `netstandard2.0` — no `record`, no `init`, no `ImplicitUsings`.

## Structural discipline

- The generator file contains ONLY the `Initialize` method and a top-level orchestration method (e.g., `ExecuteAll`).
- **NEVER put emission logic inline in the generator.** Delegate to `CodeGen/*CodeGenerator.cs` files.
- **NEVER put discovery logic inline.** Delegate to `*DiscoveryHelper.cs` or `*AttributeHelper.cs` files.
- Discovery results go in `Models/` as `internal readonly struct`, one type per file.

## Bootstrap wiring

- `NeedlrSourceGenBootstrap` (in `Generators.Attributes`) handles module-initializer registration for the main generator.
- `SourceGenRegistry` (in `NexusLabs.Needlr`) provides a decoupling layer between generated code and the core injection assembly.
- New features that produce config-bound registrations (like Options or HttpClient) piggyback on the existing `RegisterOptions` lambda — emit their code inside that method body so no new bootstrap plumbing is needed.

## Breadcrumbs

- Accept a `BreadcrumbWriter` instance from the orchestration method — never construct your own.
- Use `breadcrumbs.WriteFileHeader(builder, assemblyName, title)` for generated file headers.
- Use `breadcrumbs.WriteInlineComment(builder, indent, message)` for inline diagnostic comments.
