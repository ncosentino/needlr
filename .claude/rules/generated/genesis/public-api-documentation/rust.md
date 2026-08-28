---
# AUTO-GENERATED from .github/instructions/genesis/public-api-documentation/rust.instructions.md — do not edit
paths:
  - "**/*.rs"
---
# Rust Public API Documentation

Document externally reachable `pub` APIs with rustdoc comments. Restricted visibility such as
`pub(crate)`, `pub(super)`, or `pub(in ...)` requires documentation only when it defines a meaningful
caller boundary.

- Explain parameters, return values, ownership and lifetime expectations, errors, and non-obvious
  concurrency or lifecycle constraints.
- Add `# Errors`, `# Panics`, and `# Safety` sections when those contracts apply.
- Do not mechanically restate the item name or type signature.
