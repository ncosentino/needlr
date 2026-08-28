---
# AUTO-GENERATED from .github/instructions/needlr/hosted-ci.instructions.md — do not edit
paths:
  - ".github/workflows/**/*.yml"
  - ".github/workflows/**/*.yaml"
  - ".github/genesis-delivery.json"
  - ".github/genesis-delivery.schema.json"
  - "scripts/delivery/Configure-GitHubDelivery.ps1"
  - "scripts/test-hosted-runner-policy.ps1"
---
# Needlr hosted CI

Needlr specializes the general .NET command guidance because this public repository
uses standard GitHub-hosted runners exclusively.

- Complete suites and final validation run in GitHub Actions.
- Do not add self-hosted runner labels, PitCrew routing, runner-selection variables,
  or repository-owned runner images.
- Local iteration uses the narrow project or gate that proves the changed behavior.
