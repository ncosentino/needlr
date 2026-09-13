---
# AUTO-GENERATED from .github/instructions/registration-manifest.instructions.md — do not edit
paths:
  - "**/NexusLabs.Needlr.Generators/*.cs"
  - "**/NexusLabs.Needlr.Generators/CodeGen/**/*.cs"
  - "**/NexusLabs.Needlr.Generators/Export/**/*.cs"
  - "**/NexusLabs.Needlr.Generators/Models/RegistrationManifest/**/*.cs"
---
# Generated Registration Manifest

- Every generated DI or bootstrap registration must originate from
  `GeneratedRegistrationPlan`; do not classify registrations again for metadata.
- Update the registration-manifest serializer, schema, and red-green parity coverage
  in the same change whenever generated registration semantics change.
- Keep the manifest policy-neutral. Consumer analyzers own diagnostics and enforcement.
