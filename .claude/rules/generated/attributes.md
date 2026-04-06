---
# AUTO-GENERATED from .github/instructions/attributes.instructions.md — do not edit
paths:
  - "**/NexusLabs.Needlr.Generators.Attributes/**/*.cs"
---
# Generator Attributes Project Rules

## Target framework

This project targets `netstandard2.0`. This means:

- No `record` types
- No `init`-only property setters
- No `ImplicitUsings`
- No C# 9+ runtime features (but `LangVersion=latest` is set, so syntax features backed by the compiler work)

## Namespace

ALL types in this project use namespace `NexusLabs.Needlr.Generators` — NOT `NexusLabs.Needlr.Generators.Attributes`. The project name and the namespace do not match; this is intentional so consumers write `using NexusLabs.Needlr.Generators;` to access both attributes and their companion interfaces.

## Attributes

- Always specify `[AttributeUsage(...)]` with explicit `Inherited` and `AllowMultiple` values.
- Prefer keeping attribute surfaces minimal and frozen — grow capabilities via companion interfaces, not new attribute properties.

## Interfaces

- Keep interfaces property-only with get-only accessors.
- Consumers implement the interfaces on their own types (often records with `init` setters), which satisfies the get-only contract.

## XML documentation

All public types and members require comprehensive XML docs including `<summary>`, `<remarks>`, and `<example>` blocks with working code samples.
