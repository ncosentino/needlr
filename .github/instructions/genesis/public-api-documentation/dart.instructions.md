---
applyTo: "**/*.dart"
---

# Dart Public API Documentation

Document package-facing public declarations with Dartdoc `///` comments. Dart declarations are
public by default, but application-local types and members are not automatically package API.

- Explain parameters, return values, thrown errors, state changes, and non-obvious lifecycle,
  nullability, concurrency, or ownership constraints where applicable.
- Document reusable widget and callback contracts when callers need behavior that the type
  signature cannot express.
- Do not mechanically restate names and types already evident from the declaration.
