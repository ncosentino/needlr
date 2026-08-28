---
# AUTO-GENERATED from .github/instructions/models.instructions.md — do not edit
paths:
  - "**/NexusLabs.Needlr.Generators/Models/**/*.cs"
---
# Discovery Model Rules

## Type shape

- Use `internal readonly struct` for immutable discovery-result models.
- Use `internal enum` for kind/flag enumerations. Apply `[Flags]` when values are composable.
- Constructor parameters should match the property list exactly.

## Folder organization

Organize into logical feature subfolders when two or more related models cluster.

## Namespace

Namespace stays flat at the parent `Models` namespace (e.g., `NexusLabs.Needlr.Generators.Models`) regardless of subfolder. This is a deliberate choice to avoid consumer churn — all types are `internal` with no public API exposure.
