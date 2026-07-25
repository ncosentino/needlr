---
title: "ADR-0008: Own a version-aware Needlr agent marketplace"
status: "Accepted"
date: "2026-07-24"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "agents", "plugins", "marketplace", "ci"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Needlr has enough domain-specific behavior that a generic .NET or dependency-injection
assistant cannot reliably infer its registration conventions, source-generation
architecture, plugin lifecycles, framework integrations, and Native AOT constraints.
Installable expert agents can make that knowledge available both to Needlr contributors
and to consumers working in other repositories.

The first Needlr-specific agents were distributed by the Genesis plugin. Genesis owns
project templates, coding conventions, and scaffolding workflows rather than Needlr's
product lifecycle. That ownership split allowed agent content to drift independently of
Needlr. In particular, an Agent Framework specialist continued to describe packages and
source paths after ADR-0004 moved all AI and agentic capabilities to Foundry.

Needlr also protects `main` with required source-build, package-validation, and Native AOT
checks. Agent and marketplace files do not produce .NET binaries, NuGet packages, or AOT
executables. Running those expensive checks for an agent-only change consumes build
capacity without increasing confidence. Future behavioral evaluations have the inverse
constraint: they should run for agent changes, not for unrelated product changes.

This decision governs ownership, specialization, research behavior, and CI isolation for
Needlr's installable agent plugin. It does not define the future behavioral evaluation
suite or move Foundry-specific agents into Needlr.

## Decision drivers

- Agent guidance must evolve in the same repository and review stream as Needlr.
- Consumers must receive advice compatible with the Needlr version they use.
- Planning questions without an existing package reference still require current external
  research.
- Agent prompts must remain small enough to avoid duplicating documentation and becoming a
  second source of truth.
- Specialist routing must be clear enough that overlapping agents do not provide
  contradictory guidance.
- Protected-branch checks must remain present for every pull request while avoiding
  irrelevant source, package, AOT, or future evaluation workloads.
- The initial marketplace must be useful without claiming unearned behavioral confidence.

## Decision

The Needlr repository owns and distributes a plugin marketplace containing one `needlr`
plugin. Genesis will not remain the source of truth for Needlr-specific agents. Foundry
owns AI and Agent Framework expertise, including its optional Needlr integrations.

The Needlr plugin provides three specialists with non-overlapping primary responsibilities:

- an application specialist for public DI APIs, registration design, lifetimes, plugins,
  and version-compatible consumer guidance;
- a source-generation specialist for Needlr's compile-time architecture, analyzers, build
  integration, cross-assembly behavior, AOT, and contributor workflows;
- an integrations specialist for ASP.NET Core, Carter, SignalR, hosting, logging,
  validation, and UI framework packages.

The specialists share a thin research skill instead of embedding a frozen API catalog.
The research contract resolves evidence in this order:

1. In a Needlr source checkout, the current checkout, its instructions, accepted ADRs,
   implementation, and tests are authoritative.
2. In a consumer repository with Needlr package references, the referenced package version
   determines the matching release tag and documentation.
3. When no checkout or package reference exists because the work is still being planned,
   the agent researches current official Needlr documentation, repository sources,
   releases, and NuGet metadata before answering.

The marketplace MVP does not include a behavioral evaluation suite. Agent behavior remains
explicitly unverified until calibrated evaluations are added. The plugin layout reserves a
separate agent-evaluation scope so that later evaluations can run only for agent-related
changes.

CI classifies every change conservatively. A change is marketplace-only only when every
changed path belongs to the plugin manifests, agent profiles, shared agent skill, plugin
validator, or future agent-evaluation directory. Any unknown or mixed path runs the normal
source pipeline. The existing required branch-protection contexts remain present on every
pull request; for marketplace-only changes they complete as lightweight hosted no-ops after
marketplace validation succeeds. Scope-detection failure blocks the required checks rather
than silently skipping validation.

## Alternatives considered

### Keep the Needlr agents in Genesis

This keeps all personal developer tooling in one marketplace and avoids adding plugin files
to Needlr. It was rejected because Genesis cannot evolve atomically with Needlr APIs,
architecture decisions, and integration fixes. The existing Agent Framework drift
demonstrates that the ownership boundary is already producing incorrect guidance.

### Maintain copies in both Genesis and Needlr

This provides a transition period and lets either marketplace install the agents. It was
rejected as a steady state because duplicate agent names and independently edited prompts
create ambiguous routing and guarantee future drift. A short migration period may exist,
but Needlr becomes the sole source of truth.

### Publish one comprehensive Needlr agent

One agent is easy to discover and avoids routing decisions. It was rejected because public
application design, source-generator internals, and framework integrations require
different evidence, invariants, and boundaries. A comprehensive prompt would either become
a large duplicated manual or remain too shallow for contributor work.

### Run every validation workload for every change

This is simple and maximally conservative. It was rejected because agent-only changes
cannot affect compiled assemblies, packages, or AOT applications, while future agent
evaluations provide no evidence about unrelated source changes. Stable required contexts
with fail-closed path classification preserve merge safety without running irrelevant work.

## Consequences

### Positive

- Needlr agent behavior and product changes share ownership, review, and history.
- Downstream consumers can install Needlr expertise without installing Genesis scaffolding.
- Version-aware research avoids applying unreleased `main` APIs to older package versions.
- Planning scenarios still receive current web and repository research.
- Thin prompts rely on existing code, tests, ADRs, and documentation rather than copying
  them.
- Agent-only pull requests avoid expensive .NET, package, and AOT workloads.
- Future evaluations have an isolated path trigger that does not burden product-only
  changes.

### Negative

- Needlr gains a new published developer-tooling surface that requires maintenance.
- Three agents require disciplined descriptions and boundaries to preserve correct routing.
- Path classification becomes part of merge safety and must fail closed when uncertain.
- Required source contexts still launch lightweight no-op jobs for marketplace-only pull
  requests because branch protection requires those context names.
- The MVP agents are not behaviorally verified until the deferred evaluation suite exists.

### Neutral

- The generic C# source-generator and Roslyn-analyzer agents remain useful for raw Roslyn
  mechanics; Needlr specialists own cross-component Needlr semantics.
- Marketplace versions describe plugin distribution and do not change Needlr's NuGet
  package version.
- Removing the obsolete Genesis agents and creating the Foundry replacement are separate
  repository changes.

## Confirmation

Repository validation confirms the marketplace manifests, agent front matter, unique agent
names, shared research-skill references, and absence of known stale Needlr AI identities.

CI scope tests cover marketplace-only, source-only, mixed, unknown infrastructure, and
manual full-validation cases. A marketplace-only pull request must show marketplace
validation succeeding while the four protected source contexts complete without restore,
build, package, or AOT steps. A source-only pull request must not execute the future
agent-evaluation path.

Behavioral quality, routing accuracy, latency, and token efficiency cannot be confirmed by
the MVP repository checks. Those require the deferred calibrated evaluation suite.

## References

- ADR-0004 records that Foundry, not Needlr, owns AI and agentic runtime, workflow,
  evaluation, generator, analyzer, and provider capabilities.
- `AGENTS.md` and `.github/instructions/` remain the authoritative contributor contracts
  that specialists must load when operating inside the Needlr repository.
- `version.json`, release tags, and package references provide the version boundary used by
  consumer-mode research.
- GitHub's Copilot CLI plugin documentation defines root `plugin.json` manifests and
  recognizes marketplaces from `.claude-plugin/marketplace.json`.
