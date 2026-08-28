---
# AUTO-GENERATED from .github/instructions/genesis/public-api-documentation/go.instructions.md — do not edit
paths:
  - "**/*.go"
---
# Go Public API Documentation

Document exported declarations that form a caller-facing package contract with Go doc comments.
Begin each comment with the declared identifier and explain the contract rather than restating the
signature.

- Cover returned errors, mutation, ownership, concurrency safety, blocking behavior, and lifecycle
  constraints where applicable.
- Export solely for tooling or framework discovery does not by itself require low-value
  documentation.
