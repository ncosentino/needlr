---
# AUTO-GENERATED from .github/instructions/genesis/feature-structure.instructions.md — do not edit
paths:
  - "*.Features.*/**/*.cs"
  - "**/Features/**/*.cs"
---
# Feature structure

- Organize by business capability/vertical slice first. Do not create feature-root
  `Models`, `DTOs`, `Repositories`, `Services`, `Ids`, or `Handlers` folders.
- Keep a slice's identifiers, domain values, requests/responses, repositories,
  operations, handlers/modules, and tests together.
- Graduate a mature sub-feature into its own project only when its isolation boundary
  is stable and justified.
- Feature projects never reference sibling feature implementations directly.
- Cross-feature contracts live in the SDK/contract layer; implementations communicate
  through the selected transport. Prefer an SDK MassTransit client/consumer unless the
  accepted architecture deliberately selects HTTP or gRPC.
- Shared libraries are optional conveniences, not a mandatory layer every feature must
  route through.
- A required feature plugin lives at the feature root; a sub-slice plugin exists only
  for an isolated manual concern.
