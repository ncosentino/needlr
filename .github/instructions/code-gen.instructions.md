---
applyTo: "**/CodeGen/**/*.cs"
---

# Code Generation Emission Rules

## Class shape

- All CodeGen classes are `internal static class`.
- Entry-point methods called by the generator: `internal static`.
- Helper methods only used within the file: `private static`.

## Emission style

- Use `StringBuilder` with manual 4-space indentation via `builder.AppendLine(...)`.
- Use `GeneratorHelpers.SanitizeIdentifier(assemblyName)` for safe namespace names.
- Use `GeneratorHelpers.GetShortTypeName(typeName)` for display names in comments.

## Capability-conditional emission

When emitting code for a feature with capability interfaces (e.g., `IHttpClientTimeout`, `IHttpClientUserAgent`):

- Emit a wiring block ONLY when the discovered type actually implements the interface.
- NEVER emit dead/stub wiring for capabilities not opted into.
- Use bit-flag enums (e.g., `HttpClientCapabilities`) to pass capability detection results from discovery to emission.

This is the load-bearing extensibility pattern: future capabilities ship as new interfaces + new conditional emission blocks, with zero impact on existing consumers.

## Generated code targets

The generated C# must compile on the **consumer's** target framework (typically `net10.0`), which differs from the generator's `netstandard2.0`. Use `global::` prefixes for all type references in emitted code to avoid namespace collisions.
