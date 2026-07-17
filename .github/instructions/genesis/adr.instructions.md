---
applyTo: "**/adr-*.md,**/ADR-*.md,**/*-adr-*.md,**/*-ADR-*.md,**/*-adr.md,**/*-ADR.md"
---

# Architecture Decision Record Authoring

Use these rules for every ADR, regardless of where the repository stores it.

## Purpose

An ADR records one architecturally significant decision for future readers who were
not part of the original discussion. It must explain the problem, decision, rationale,
scope, alternatives, and accepted consequences without requiring access to source code,
an issue, a pull request, or conversation history.

Record why the decision was made, not the chronology of how it was implemented.

## Metadata and naming

The default location is `docs/adr/`. Use `adr-NNNN-title-slug.md` with sequential
4-digit numbering, a lowercase hyphenated slug, and this front matter:

```yaml
---
title: "ADR-NNNN: Decision title"
status: "Proposed"
date: "YYYY-MM-DD"
authors: []
tags: ["architecture", "decision"]
supersedes: ""
superseded_by: ""
---
```

## Status lifecycle

| Status | Meaning |
|--------|---------|
| Proposed | Decision documented, awaiting review or acceptance |
| Accepted | Decision approved and in effect |
| Rejected | Decision was considered but not adopted |
| Superseded | Replaced by a newer ADR (set `superseded_by` field) |
| Deprecated | No longer relevant due to changed circumstances |

## Decision history, not work history

An ADR preserves how a decision evolved, not the activity used to implement it.

- A Proposed ADR may change while the decision is under review.
- Once Accepted, keep its context, decision, rationale, and consequences unchanged.
- A short dated outcome note may record observed consequences or confirmation results.
- A material change requires a new ADR linked through `supersedes` and `superseded_by`.
- Issues and PRs track tasks, owners, progress, commits, and rollout activity. They may
  be supplemental references, but the ADR must not depend on them for its meaning.

## Required content

Every ADR must make the following reasoning explicit.

### Context and scope

Describe the facts, forces, constraints, and problem that require a decision. State
which systems, components, interfaces, or quality attributes the decision governs and
what is explicitly out of scope. Distinguish verified facts from assumptions.

### Decision drivers

List the criteria that determine a good outcome, such as reliability, reversibility,
operational cost, delivery constraints, security, or maintainability. Use these drivers
to explain why the selected option won.

### Decision

State the chosen direction as a clear commitment. Explain how it satisfies the decision
drivers and where the decision applies. Do not leave the decision implicit in a list of
implementation details.

### Alternatives considered

Record the serious alternatives, including the status quo when it was viable. For each,
explain the relevant benefits, drawbacks, and why it lost against the decision drivers.
Do not invent filler alternatives or enforce an arbitrary count.

### Consequences

Document positive, negative, and neutral consequences. Be explicit about tradeoffs,
risks, follow-on constraints, and what becomes harder. Do not hide drawbacks to make the
decision appear stronger.

### Confirmation

When the decision can be checked, explain how compliance or continued validity will be
confirmed through tests, architecture rules, configuration, operational evidence, or a
manual review. State any evidence that cannot be verified from the repository.

### References

List related ADRs and supplemental source material when useful. Give each reference
enough description for the reader to understand why it is relevant.

## Evidence must be understandable

The ADR must contain the meaning of its evidence. A file path, symbol, package, issue,
or link is a locator, not an explanation.

- Do not use a bare citation such as `src/App/Startup.cs:42-71` as rationale.
- Explain what the referenced artifact demonstrates and why that fact matters to the
  decision.
- Prefer stable identifiers such as a named type, method, module, package, configuration
  key, or architectural boundary over mutable line ranges.
- When an exact historical snapshot matters, use an immutable commit permalink and label
  it as evidence from that point in time. Still summarize the evidence in the ADR.
- Keep essential reasoning in the ADR. References are supplemental and must not be
  required to understand the decision.

Instead of writing only `Evidence: src/App/Startup.cs:42-71`, explain the fact and its
significance: `StartupPlugin constructs every adapter directly, so adding an adapter
changes the application composition root. StartupPlugin.ConfigureServices contains the
current implementation.`

## Write as a permanent record

- Keep one decision per ADR and default to roughly one or two pages.
- Use complete, factual sentences. Bullets may organize reasoning but must not replace it
  with fragments.
- Do not turn the ADR into an implementation plan or design guide.
- Do not mention planning phases, sprints, conversation history, or what "the user" said.
- If an event motivated the decision, identify it by date and substance.
- Check existing ADRs before creating a new one; update supersession links instead of
  silently contradicting or rewriting accepted decisions.
