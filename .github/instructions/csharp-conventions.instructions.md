---
applyTo: "**/*.cs"
---

# Needlr C# Conventions

- Default implementation types to `internal`; expose only caller-facing contracts.
- Runtime data carriers use records; Roslyn discovery models are the immutable-struct
  exception.
- Instantiable non-services require `[DoNotAutoRegister]`; constructor shape,
  visibility, generic arity, and namespaces do not suppress Needlr registration.
- Prefer composition for runtime services, and never store service instances in static
  fields.
