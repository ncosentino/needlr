---
title: "ADR-0004: Extract the AI platform to Foundry"
status: "Accepted"
date: "2026-07-17"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "repository", "packages", "foundry"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Needlr is an opinionated dependency-injection, plugin, and source-generation
framework. Its AI and agentic packages grew into a separate platform covering
Microsoft Agent Framework, Microsoft.Extensions.AI evaluation, experiment
execution, MEAI Reporting, Langfuse, GitHub Copilot, Semantic Kernel,
diagnostics, workflows, testing, generators, and analyzers.

Before this decision, the AI packages represented approximately half of the
repository's production C# code and shared Needlr's version, release workflow,
dependency policy, documentation, and issue surface. Non-AI Needlr production
packages did not depend on those packages. The dependency direction was already
AI-to-Needlr rather than Needlr-to-AI.

This decision governs repository and package ownership. It does not change the
generic extension points that Needlr exposes to external integrations.

## Decision drivers

- Needlr must retain a focused dependency-injection and plugin identity.
- Fast-moving AI provider dependencies require an independent release cadence.
- AI runtime packages must be usable without adopting Needlr.
- Needlr integration must exercise the same public extension model available to
  other external packages.
- The alpha package line permits a clean break without compatibility shims.

## Decision

Needlr will not own AI or agentic runtime, provider, workflow, evaluation,
testing, generator, or analyzer packages.

Those capabilities move to the Foundry repository at
`https://github.com/ncosentino/foundry`.

Foundry owns neutral AI packages and explicit
`NexusLabs.Foundry.Needlr.*` integration packages. Foundry may depend on
released Needlr packages. Needlr must not depend on Foundry.

Needlr retains only generic dependency-injection, plugin, source-generation,
web, and framework-integration extension points. A Foundry requirement may
justify a Needlr change only when the resulting capability is generally useful
to external integrations and contains no AI-specific behavior.

The former `NexusLabs.Needlr.AgentFramework`,
`NexusLabs.Needlr.Copilot`, and `NexusLabs.Needlr.SemanticKernel`
alpha package lines will not receive forwarding packages, compatibility
wrappers, or replacement namespaces in this repository.

## Alternatives considered

### Keep the AI platform in Needlr

This preserves one repository and atomic source-generator changes. It was
rejected because it couples two products with different users, dependencies,
release cadences, and operational risks.

### Keep only Needlr-specific AI adapters in Needlr

This leaves less code in Needlr while preserving close ownership of the
integration. It was rejected because the adapters, generators, analyzers, and
provider-specific declarations evolve with Foundry. Splitting their ownership
would retain cross-repository release coordination and AI-specific code inside
Needlr.

### Preserve the old package identities from Foundry

This reduces consumer migration work. It was rejected because neutral Foundry
packages would continue to present themselves as Needlr capabilities, leaving
the product boundary unresolved.

## Consequences

### Positive

- Needlr returns to a coherent DI and plugin scope.
- Needlr releases no longer depend on MAF, MEAI, Langfuse, Copilot, Semantic
  Kernel, or their preview and telemetry dependencies.
- Foundry can version and release independently.
- The external Foundry integration validates Needlr's public plugin and
  source-generation extension model.

### Negative

- Existing alpha consumers must change package references, namespaces, and
  selected APIs.
- Cross-repository compatibility requires explicit CI coverage.
- Needlr and Foundry maintain separate build, release, and documentation
  infrastructure.

### Neutral

- Historical Needlr releases remain available but are not compatibility
  surfaces.
- AI-specific architecture records move with Foundry; this record explains why
  they no longer live in Needlr.

## Confirmation

The decision is confirmed when:

- Needlr builds and packages without AI projects or AI provider dependencies;
- Needlr's release workflow publishes only DI, plugin, web, and generic
  integration packages;
- Foundry builds its neutral packages without Needlr references;
- Foundry's Needlr integration builds against released Needlr packages through
  a one-way dependency;
- representative consumers migrate to Foundry without retaining duplicate
  orchestration or evaluation infrastructure.

Repository checks cannot prove model quality or external provider availability.
Those remain Foundry and consumer operational concerns.

## References

- The Foundry repository owns the extracted AI source history and its continuing
  architecture decisions.
- Foundry ADR-0004 records the reciprocal product and package boundary.
