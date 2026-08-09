# Architecture decisions

Architecture Decision Records preserve significant decisions that future contributors
must understand without reconstructing issue, pull-request, or conversation history.

## When to write one

Use an ADR when a choice changes system structure, technology, integration boundaries,
important quality attributes, or another convention that is costly to reverse.

Routine implementation choices, direct bug fixes, and decisions already covered by an
accepted record do not need another ADR.

## What the record owns

One ADR explains:

- the facts, constraints, assumptions, and scope;
- the decision drivers;
- the chosen direction;
- serious alternatives and why they lost;
- positive, negative, and neutral consequences;
- how continued validity or compliance can be checked.

The record owns decision meaning, not task history. Issues and pull requests own work,
owners, rollout progress, and commits.

## Lifecycle

Proposed records may change during review. Once accepted, preserve their original
context, decision, rationale, and consequences.

A material change creates a new ADR and links both records through supersession
metadata. Rewriting an accepted ADR would erase the constraints future readers need to
understand the old decision.

## Evidence

Paths, symbols, packages, and links are locators. The ADR explains what each artifact
demonstrates and why it matters.

Essential reasoning stays in the record. External references are supplemental, not a
prerequisite for understanding the decision.
