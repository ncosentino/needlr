---
# AUTO-GENERATED from .github/instructions/genesis/public-api-documentation/javascript-typescript.instructions.md — do not edit
paths:
  - "**/*.js"
  - "**/*.jsx"
  - "**/*.mjs"
  - "**/*.cjs"
  - "**/*.ts"
  - "**/*.tsx"
---
# JavaScript and TypeScript Public API Documentation

Document externally consumable package exports and reusable caller-facing contracts with JSDoc.
An `export` used only for application-local composition, framework discovery, or tests is not
automatically public API.

- Cover type parameters, parameters, return or yield values, rejected or thrown errors, and
  non-obvious lifecycle, concurrency, nullability, or ownership constraints where applicable.
- Document exported types and component contracts when consumers need behavior that the type
  signature cannot express.
- Do not mechanically restate names and types already evident from the declaration.
