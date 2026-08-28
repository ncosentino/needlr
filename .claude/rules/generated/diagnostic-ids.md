---
# AUTO-GENERATED from .github/instructions/diagnostic-ids.instructions.md — do not edit
paths:
  - "**/*Analyzer.cs"
  - "**/DiagnosticIds.cs"
  - "**/DiagnosticDescriptors.cs"
  - "**/AnalyzerReleases.*.md"
---
# Needlr Diagnostic IDs

Use the component prefix already owned by the analyzer package:

- `NDLRCOR` — core DI analyzers
- `NDLRGEN` — source-generator analyzers
- `NDLRHTTP` — HTTP client analyzers
- `NDLRLOG` — logging analyzers
- `NDLRSIG` — SignalR analyzers

Allocate the next unused numeric suffix in that component and never reuse a retired ID.
