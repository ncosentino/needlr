---
applyTo: "**/*.cs"
---

# C# Public API Documentation

Document every caller-facing public type and member with XML documentation. Describe the contract
a caller or implementer needs rather than restating the signature or implementation.

- Cover type parameters, parameters, return values, observable exceptions, and non-obvious
  lifecycle, concurrency, nullability, or ownership constraints where applicable.
- Use `<inheritdoc />` when an inherited contract remains accurate; document only meaningful
  differences.
- Public visibility required solely for framework activation or generated code does not by itself
  require hand-authored documentation.
