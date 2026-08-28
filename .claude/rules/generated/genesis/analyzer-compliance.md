---
# AUTO-GENERATED from .github/instructions/genesis/analyzer-compliance.instructions.md — do not edit
paths:
  - "**/*.cs"
---
# Analyzer Compliance Rules

These templates enable strict analyzers as build-breaking errors (warnings-as-errors). Write to these on the first pass to avoid a fix → rebuild cycle per rule.

- **Use file-scoped namespaces (`IDE0161`).** Declare C# namespaces as `namespace Acme.Feature;`, not with block-scoped namespace braces.
- **Mark never-reassigned locals as `const` (Roslynator `RCS1118`).** A local that is assigned once and never reassigned must be declared `const` — e.g. `const int expected = 5;`, not `var expected = 5;`.
- **Never give a type the same leaf name as a namespace segment (`CS0118`).** A `Calculation` type inside (or referenced alongside) an `Acme.Calculation` namespace makes the bare name ambiguous. Use a distinct type name (e.g. `CalculationResult`) or always fully-qualify.
